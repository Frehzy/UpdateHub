using Microsoft.Extensions.Logging.Abstractions;
using UpdateHub.Server.Application.Repositories;
using UpdateHub.Server.Application.Services;
using UpdateHub.Server.Application.Sync;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;
using UpdateHub.Tests.TestSupport;

namespace UpdateHub.Tests.Application.Services;

/// <summary>
/// Проверяет разграничение доступа к компьютерам.
/// </summary>
/// <remarks>
/// Единственное место, где принимается решение о допуске клиента. Раньше эта
/// проверка жила в middleware, который искал идентификатор компьютера по имени
/// поля, отсутствующему в моделях, и потому отклонял любое обращение обычного
/// пользователя. Здесь проверяются все исходы, включая те, что раньше
/// не работали вовсе: блокировка компьютера и доступ через группу.
/// </remarks>
public class ClientAccessServiceTests : IDisposable
{
    private readonly TestDatabase _database;
    private readonly ClientAccessService _service;

    /// <summary>Готовит базу и службу.</summary>
    public ClientAccessServiceTests()
    {
        _database = new TestDatabase();
        _service = CreateService(_database.Context);
    }

    /// <summary>Собирает службу поверх заданного контекста.</summary>
    /// <param name="context">Контекст базы данных.</param>
    /// <returns>Готовая служба.</returns>
    private static ClientAccessService CreateService(AppDbContext context)
        => new(
            new ClientRepository(context),
            new UserClientAccessRepository(context),
            new UserGroupAccessRepository(context),
            new ClientBlockHistoryRepository(context),
            NullLogger<ClientAccessService>.Instance);

    /// <summary>Заводит компьютер в базе.</summary>
    /// <param name="id">Идентификатор компьютера.</param>
    /// <param name="groupId">Группа компьютера.</param>
    /// <param name="isActive">Признак активности.</param>
    /// <param name="isBlocked">Признак блокировки.</param>
    private async Task AddClientAsync(string id, string? groupId = null, bool isActive = true, bool isBlocked = false)
    {
        _database.Context.Clients.Add(new ClientEntity
        {
            Id = id,
            GroupId = groupId,
            IsActive = isActive,
            IsBlocked = isBlocked
        });

        await _database.Context.SaveChangesAsync();
    }

    /// <summary>
    /// Незарегистрированный компьютер отклоняется, а не заводится молча.
    /// Автоматическая регистрация позволяла бы любому пользователю засорять
    /// таблицу произвольными идентификаторами.
    /// </summary>
    [Fact]
    public async Task AuthorizeAsync_НеизвестныйКомпьютер_Отклоняется()
    {
        var result = await _service.AuthorizeAsync("user-1", isAdmin: false, "нет-такого");

        Assert.False(result.IsAllowed);
        Assert.Equal(ClientAccessOutcome.UnknownClient, result.Outcome);
        Assert.NotNull(result.Reason);
    }

    /// <summary>Администратору незарегистрированный компьютер тоже недоступен.</summary>
    [Fact]
    public async Task AuthorizeAsync_НеизвестныйКомпьютерУАдминистратора_Отклоняется()
    {
        var result = await _service.AuthorizeAsync("admin-1", isAdmin: true, "нет-такого");

        Assert.Equal(ClientAccessOutcome.UnknownClient, result.Outcome);
    }

    /// <summary>Помеченный удалённым компьютер считается несуществующим.</summary>
    [Fact]
    public async Task AuthorizeAsync_УдалённыйКомпьютер_Отклоняется()
    {
        await AddClientAsync("pc-1", isActive: false);

        var result = await _service.AuthorizeAsync("user-1", isAdmin: true, "pc-1");

        Assert.Equal(ClientAccessOutcome.UnknownClient, result.Outcome);
    }

    /// <summary>
    /// Заблокированный компьютер отклоняется даже у администратора.
    /// Раньше признак блокировки выставлялся, но на пути запроса не читался
    /// нигде, и заблокированный компьютер продолжал качать обновления.
    /// </summary>
    [Fact]
    public async Task AuthorizeAsync_ЗаблокированныйКомпьютер_ОтклоняетсяДажеУАдминистратора()
    {
        await AddClientAsync("pc-1", isBlocked: true);

        var result = await _service.AuthorizeAsync("admin-1", isAdmin: true, "pc-1");

        Assert.False(result.IsAllowed);
        Assert.Equal(ClientAccessOutcome.Blocked, result.Outcome);
    }

    /// <summary>Причина блокировки попадает в ответ, чтобы её увидел пользователь.</summary>
    [Fact]
    public async Task AuthorizeAsync_ЗаблокированныйКомпьютер_ВозвращаетПричину()
    {
        await AddClientAsync("pc-1", isBlocked: true);
        _database.Context.ClientBlockHistories.Add(new ClientBlockHistoryEntity
        {
            ClientId = "pc-1",
            Action = "blocked",
            Reason = "выведен из эксплуатации",
            CreatedAt = DateTime.UtcNow
        });
        await _database.Context.SaveChangesAsync();

        var result = await _service.AuthorizeAsync("user-1", isAdmin: false, "pc-1");

        Assert.Equal(ClientAccessOutcome.Blocked, result.Outcome);
        Assert.Contains("выведен из эксплуатации", result.Reason!, StringComparison.Ordinal);
    }

    /// <summary>Администратор получает доступ к любому незаблокированному компьютеру.</summary>
    [Fact]
    public async Task AuthorizeAsync_Администратор_ПолучаетДоступБезЯвныхПрав()
    {
        await AddClientAsync("pc-1");

        var result = await _service.AuthorizeAsync("admin-1", isAdmin: true, "pc-1");

        Assert.True(result.IsAllowed);
    }

    /// <summary>Пользователь без прав получает отказ.</summary>
    [Fact]
    public async Task AuthorizeAsync_ПользовательБезПрав_ПолучаетОтказ()
    {
        await AddClientAsync("pc-1");

        var result = await _service.AuthorizeAsync("user-1", isAdmin: false, "pc-1");

        Assert.False(result.IsAllowed);
        Assert.Equal(ClientAccessOutcome.Forbidden, result.Outcome);
    }

    /// <summary>Персональное разрешение открывает доступ к компьютеру.</summary>
    [Fact]
    public async Task AuthorizeAsync_ПерсональноеРазрешение_ОткрываетДоступ()
    {
        await AddClientAsync("pc-1");
        _database.Context.UserClientAccesses.Add(new UserClientAccessEntity { UserId = "user-1", ClientId = "pc-1" });
        await _database.Context.SaveChangesAsync();

        var result = await _service.AuthorizeAsync("user-1", isAdmin: false, "pc-1");

        Assert.True(result.IsAllowed);
    }

    /// <summary>
    /// Разрешение на группу открывает доступ ко всем её компьютерам —
    /// ради этого группы и заведены.
    /// </summary>
    [Fact]
    public async Task AuthorizeAsync_РазрешениеНаГруппу_ОткрываетДоступКЕёКомпьютерам()
    {
        _database.Context.Groups.Add(new GroupEntity { Id = "group-1", Name = "Бухгалтерия" });
        await _database.Context.SaveChangesAsync();
        await AddClientAsync("pc-1", groupId: "group-1");

        _database.Context.UserGroupAccesses.Add(new UserGroupAccessEntity { UserId = "user-1", GroupId = "group-1" });
        await _database.Context.SaveChangesAsync();

        var result = await _service.AuthorizeAsync("user-1", isAdmin: false, "pc-1");

        Assert.True(result.IsAllowed);
    }

    /// <summary>
    /// Разрешение на одну группу не открывает компьютеры другой группы.
    /// Проверка парная к предыдущей: важно, что права не «протекают».
    /// </summary>
    [Fact]
    public async Task AuthorizeAsync_РазрешениеНаДругуюГруппу_ДоступНеОткрывает()
    {
        _database.Context.Groups.Add(new GroupEntity { Id = "group-1", Name = "Бухгалтерия" });
        _database.Context.Groups.Add(new GroupEntity { Id = "group-2", Name = "Склад" });
        await _database.Context.SaveChangesAsync();
        await AddClientAsync("pc-1", groupId: "group-2");

        _database.Context.UserGroupAccesses.Add(new UserGroupAccessEntity { UserId = "user-1", GroupId = "group-1" });
        await _database.Context.SaveChangesAsync();

        var result = await _service.AuthorizeAsync("user-1", isAdmin: false, "pc-1");

        Assert.Equal(ClientAccessOutcome.Forbidden, result.Outcome);
    }

    /// <summary>Пользователь без единого разрешения не проходит общую проверку.</summary>
    [Fact]
    public async Task HasAnyAccessAsync_БезРазрешений_ВозвращаетЛожь()
    {
        Assert.False(await _service.HasAnyAccessAsync("user-1"));
    }

    /// <summary>Достаточно одного разрешения — персонального или на группу.</summary>
    [Fact]
    public async Task HasAnyAccessAsync_ЕстьРазрешение_ВозвращаетИстину()
    {
        await AddClientAsync("pc-1");
        _database.Context.UserClientAccesses.Add(new UserClientAccessEntity { UserId = "user-1", ClientId = "pc-1" });
        _database.Context.UserGroupAccesses.Add(new UserGroupAccessEntity { UserId = "user-2", GroupId = "group-1" });
        await _database.Context.SaveChangesAsync();

        Assert.True(await _service.HasAnyAccessAsync("user-1"));
        Assert.True(await _service.HasAnyAccessAsync("user-2"));
    }

    /// <summary>Освобождает базу.</summary>
    public void Dispose() => _database.Dispose();
}
