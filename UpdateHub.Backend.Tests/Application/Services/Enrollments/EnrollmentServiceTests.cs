using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UpdateHub.Backend.Tests.TestSupport;
using UpdateHub.BackendServer.Application.Abstractions.Services.Enrollments;
using UpdateHub.BackendServer.Application.Repositories.Clients;
using UpdateHub.BackendServer.Application.Repositories.Enrollments;
using UpdateHub.BackendServer.Application.Repositories.Groups;
using UpdateHub.BackendServer.Application.Services.Enrollments;
using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Domain.Entities.Clients;
using UpdateHub.BackendServer.Domain.Entities.Groups;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Backend.Tests.Application.Services.Enrollments;

/// <summary>
/// Проверяет рассмотрение заявок на регистрацию компьютеров.
/// </summary>
/// <remarks>
/// Через эту службу компьютер попадает в базу, и это единственный путь, которым
/// машина вводится в работу без участия администратора за консолью. Косвенно она
/// была покрыта проверками контроллеров, но те идут по благополучному пути:
/// подал — одобрили — работает.
/// <para>
/// Здесь проверяются переходы состояний, где и живут ошибки: одобрить дважды,
/// одобрить отклонённую, одобрить для уже существующего компьютера, подать
/// заявку повторно. Ошибка в любом из них означает либо второй компьютер
/// с тем же идентификатором, либо потерянные права у существующего.
/// </para>
/// </remarks>
public class EnrollmentServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FakeClientService _clientService = new();
    private readonly EnrollmentService _service;

    /// <summary>Готовит базу и службу заявок.</summary>
    public EnrollmentServiceTests()
    {
        var context = _database.Context;

        _service = new EnrollmentService(
            new EnrollmentRequestRepository(context),
            new ClientRepository(context),
            new ClientComputerInfoRepository(context),
            new GroupRepository(context),
            _clientService,
            NullLogger<EnrollmentService>.Instance);
    }

    /// <summary>Собирает заявку с заданным идентификатором компьютера.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="hostname">Имя машины.</param>
    /// <returns>Заявка.</returns>
    private static EnrollmentSubmission Submission(
        string clientId = "pc-zayavka",
        string? hostname = "buhgalteriya-1")
        => new(
            ClientId: clientId,
            HardwareFingerprint: "otpechatok",
            Hostname: hostname,
            OsVersion: "Astra Linux 1.7",
            Username: "ivanov",
            Comment: "первичная установка");

    /// <summary>Заявка сохраняется со сведениями о машине и в состоянии «подана».</summary>
    [Fact]
    public async Task SubmitAsync_StoresRequestAsPending()
    {
        var request = await _service.SubmitAsync(Submission(), remoteIpAddress: "192.168.1.10");

        Assert.Equal("pc-zayavka", request.ClientId);
        Assert.Equal("buhgalteriya-1", request.Hostname);
        Assert.Equal("192.168.1.10", request.RemoteIpAddress);
        Assert.Equal(EnrollmentStatus.Pending, request.Status);

        using var context = _database.CreateSeparateContext();
        Assert.Single(context.EnrollmentRequests);
    }

    /// <summary>Пустой идентификатор компьютера отклоняется.</summary>
    [Fact]
    public async Task SubmitAsync_EmptyClientId_Rejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.SubmitAsync(Submission(clientId: "   "), remoteIpAddress: null));
    }

    /// <summary>
    /// Повторная подача с той же машины не плодит заявки, а обновляет поданную.
    /// </summary>
    /// <remarks>
    /// Клиент вызывает <c>updatehub enroll</c> столько раз, сколько человек
    /// решит попробовать. Без этого администратор получил бы десяток одинаковых
    /// заявок на один компьютер и разбирался бы, какую одобрять.
    /// </remarks>
    [Fact]
    public async Task SubmitAsync_SameClientTwice_UpdatesSingleRequest()
    {
        var first = await _service.SubmitAsync(Submission(hostname: "staroe-imya"), remoteIpAddress: null);
        var second = await _service.SubmitAsync(Submission(hostname: "novoe-imya"), remoteIpAddress: "10.0.0.5");

        Assert.Equal(first.Id, second.Id);

        using var context = _database.CreateSeparateContext();
        var stored = Assert.Single(context.EnrollmentRequests);

        // Сведения обновились: администратор видит актуальное состояние машины.
        Assert.Equal("novoe-imya", stored.Hostname);
        Assert.Equal("10.0.0.5", stored.RemoteIpAddress);
    }

    /// <summary>Одобрение заводит компьютер и сведения о его железе.</summary>
    [Fact]
    public async Task ApproveAsync_CreatesClientWithComputerInfo()
    {
        var request = await _service.SubmitAsync(Submission(), remoteIpAddress: null);

        var client = await _service.ApproveAsync(request.Id, groupId: null, resolvedBy: "admin");

        Assert.Equal("pc-zayavka", client.Id);
        Assert.True(client.IsActive);

        using var context = _database.CreateSeparateContext();
        var stored = await context.Clients
            .Include(item => item.ComputerInfo)
            .SingleAsync();

        Assert.Equal("buhgalteriya-1", stored.ComputerInfo?.Hostname);
        Assert.Equal("Astra Linux 1.7", stored.ComputerInfo?.OsVersion);
    }

    /// <summary>Одобрение отмечает заявку рассмотренной и записывает, кем.</summary>
    [Fact]
    public async Task ApproveAsync_MarksRequestResolved()
    {
        var request = await _service.SubmitAsync(Submission(), remoteIpAddress: null);

        await _service.ApproveAsync(request.Id, groupId: null, resolvedBy: "admin");

        using var context = _database.CreateSeparateContext();
        var stored = Assert.Single(context.EnrollmentRequests);

        Assert.Equal(EnrollmentStatus.Approved, stored.Status);
        Assert.Equal("admin", stored.ResolvedBy);
        Assert.NotNull(stored.ResolvedAt);
    }

    /// <summary>Одобрение помещает компьютер в указанную группу.</summary>
    [Fact]
    public async Task ApproveAsync_WithGroup_PlacesClientInIt()
    {
        var group = new GroupEntity { Name = "Бухгалтерия" };
        _database.Context.Groups.Add(group);
        await _database.Context.SaveChangesAsync();

        var request = await _service.SubmitAsync(Submission(), remoteIpAddress: null);

        var client = await _service.ApproveAsync(request.Id, group.Id, resolvedBy: "admin");

        Assert.Equal(group.Id, client.GroupId);
    }

    /// <summary>Одобрение в несуществующую группу отклоняется, компьютер не заводится.</summary>
    /// <remarks>
    /// Важна вторая половина: заявка обязана остаться поданной. Иначе опечатка
    /// в идентификаторе группы сожгла бы её, и машине пришлось бы подавать снова.
    /// </remarks>
    [Fact]
    public async Task ApproveAsync_UnknownGroup_RejectedAndRequestUntouched()
    {
        var request = await _service.SubmitAsync(Submission(), remoteIpAddress: null);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _service.ApproveAsync(request.Id, "gruppy-net", resolvedBy: "admin"));

        using var context = _database.CreateSeparateContext();
        Assert.Empty(context.Clients);
        Assert.Equal(EnrollmentStatus.Pending, Assert.Single(context.EnrollmentRequests).Status);
    }

    /// <summary>Повторное одобрение той же заявки отклоняется.</summary>
    /// <remarks>
    /// Двойное нажатие в панели — обычное дело. Без этой проверки второе
    /// одобрение прошло бы и перезаписало группу компьютера, а в историю
    /// добавилась бы вторая запись о регистрации.
    /// </remarks>
    [Fact]
    public async Task ApproveAsync_Twice_Rejected()
    {
        var request = await _service.SubmitAsync(Submission(), remoteIpAddress: null);
        await _service.ApproveAsync(request.Id, groupId: null, resolvedBy: "admin");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ApproveAsync(request.Id, groupId: null, resolvedBy: "admin"));
    }

    /// <summary>Одобрить отклонённую заявку нельзя.</summary>
    [Fact]
    public async Task ApproveAsync_AfterReject_Rejected()
    {
        var request = await _service.SubmitAsync(Submission(), remoteIpAddress: null);
        await _service.RejectAsync(request.Id, resolvedBy: "admin");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ApproveAsync(request.Id, groupId: null, resolvedBy: "admin"));

        using var context = _database.CreateSeparateContext();
        Assert.Empty(context.Clients);
    }

    /// <summary>
    /// Одобрение для уже заведённого компьютера возвращает его в строй,
    /// а не создаёт второй.
    /// </summary>
    /// <remarks>
    /// Это путь восстановления после переустановки системы: администратор
    /// удалил компьютер, машина подала заявку снова. Идентификатор тот же —
    /// он лежит в /etc/updatehub/client-id и переустановку переживает.
    /// Второй записи с тем же ключом база и не допустила бы, а вот потерять
    /// выданные права, заведя всё заново, было бы легко.
    /// </remarks>
    [Fact]
    public async Task ApproveAsync_ExistingDeletedClient_RestoresIt()
    {
        _database.Context.Clients.Add(new ClientEntity
        {
            Id = "pc-zayavka",
            IsActive = false
        });
        await _database.Context.SaveChangesAsync();

        var request = await _service.SubmitAsync(Submission(), remoteIpAddress: null);

        var client = await _service.ApproveAsync(request.Id, groupId: null, resolvedBy: "admin");

        Assert.True(client.IsActive);

        using var context = _database.CreateSeparateContext();
        Assert.Single(context.Clients);
    }

    /// <summary>Одобрение неизвестной заявки отклоняется.</summary>
    [Fact]
    public async Task ApproveAsync_UnknownRequest_Rejected()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _service.ApproveAsync("zayavki-net", groupId: null, resolvedBy: "admin"));
    }

    /// <summary>Отклонение отмечает заявку и не заводит компьютер.</summary>
    [Fact]
    public async Task RejectAsync_MarksRequestAndCreatesNoClient()
    {
        var request = await _service.SubmitAsync(Submission(), remoteIpAddress: null);

        await _service.RejectAsync(request.Id, resolvedBy: "admin");

        using var context = _database.CreateSeparateContext();
        var stored = Assert.Single(context.EnrollmentRequests);

        Assert.Equal(EnrollmentStatus.Rejected, stored.Status);
        Assert.Equal("admin", stored.ResolvedBy);
        Assert.Empty(context.Clients);
    }

    /// <summary>Повторное отклонение отклоняется.</summary>
    [Fact]
    public async Task RejectAsync_Twice_Rejected()
    {
        var request = await _service.SubmitAsync(Submission(), remoteIpAddress: null);
        await _service.RejectAsync(request.Id, resolvedBy: "admin");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.RejectAsync(request.Id, resolvedBy: "admin"));
    }

    /// <summary>
    /// После отклонения машина может подать заявку заново.
    /// </summary>
    /// <remarks>
    /// Отклонение не должно закрывать путь навсегда: администратор мог
    /// отклонить по ошибке или до выяснения. Прежняя заявка ищется только
    /// среди поданных, поэтому новая создаётся отдельной.
    /// </remarks>
    [Fact]
    public async Task SubmitAsync_AfterReject_CreatesNewRequest()
    {
        var first = await _service.SubmitAsync(Submission(), remoteIpAddress: null);
        await _service.RejectAsync(first.Id, resolvedBy: "admin");

        var second = await _service.SubmitAsync(Submission(), remoteIpAddress: null);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(EnrollmentStatus.Pending, second.Status);

        using var context = _database.CreateSeparateContext();
        Assert.Equal(2, await context.EnrollmentRequests.CountAsync());
    }

    /// <summary>Освобождает базу.</summary>
    public void Dispose()
    {
        _database.Dispose();
        GC.SuppressFinalize(this);
    }
}
