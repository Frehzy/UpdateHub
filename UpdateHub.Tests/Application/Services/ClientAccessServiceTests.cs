using Microsoft.Extensions.Logging.Abstractions;
using UpdateHub.Server.Application.Repositories;
using UpdateHub.Server.Application.Services;
using UpdateHub.Server.Application.Sync;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Domain.Enums;
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
        var context = _database.Context;

        _service = new ClientAccessService(
            new ClientRepository(context),
            new UserClientAccessRepository(context),
            new UserGroupAccessRepository(context),
            new ClientBlockHistoryRepository(context),
            NullLogger<ClientAccessService>.Instance);
    }

    /// <summary>
    /// Заводит пользователя.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <returns>Идентификатор созданного пользователя.</returns>
    /// <remarks>
    /// Разрешения ссылаются на пользователя внешним ключом, поэтому без записи
    /// в таблице пользователей вставка разрешения не проходит.
    /// </remarks>
    private async Task<string> AddUserAsync(string userId)
    {
        _database.Context.Users.Add(new UserEntity
        {
            Id = userId,
            Username = userId,
            PasswordHash = "не-проверяется-в-этих-тестах",
            Role = UserRole.Client,
            IsActive = true
        });

        await _database.Context.SaveChangesAsync();
        return userId;
    }

    /// <summary>Заводит группу компьютеров.</summary>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="name">Название группы.</param>
    private async Task AddGroupAsync(string groupId, string name)
    {
        _database.Context.Groups.Add(new GroupEntity { Id = groupId, Name = name });
        await _database.Context.SaveChangesAsync();
    }

    /// <summary>Заводит компьютер.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="groupId">Группа компьютера.</param>
    /// <param name="isActive">Признак активности.</param>
    /// <param name="isBlocked">Признак блокировки.</param>
    private async Task AddClientAsync(
        string clientId,
        string? groupId = null,
        bool isActive = true,
        bool isBlocked = false)
    {
        _database.Context.Clients.Add(new ClientEntity
        {
            Id = clientId,
            GroupId = groupId,
            IsActive = isActive,
            IsBlocked = isBlocked
        });

        await _database.Context.SaveChangesAsync();
    }

    /// <summary>Выдаёт пользователю персональное разрешение на компьютер.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="clientId">Идентификатор компьютера.</param>
    private async Task GrantClientAccessAsync(string userId, string clientId)
    {
        _database.Context.UserClientAccesses.Add(new UserClientAccessEntity
        {
            UserId = userId,
            ClientId = clientId
        });

        await _database.Context.SaveChangesAsync();
    }

    /// <summary>Выдаёт пользователю разрешение на группу.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="groupId">Идентификатор группы.</param>
    private async Task GrantGroupAccessAsync(string userId, string groupId)
    {
        _database.Context.UserGroupAccesses.Add(new UserGroupAccessEntity
        {
            UserId = userId,
            GroupId = groupId
        });

        await _database.Context.SaveChangesAsync();
    }

    /// <summary>
    /// Незарегистрированный компьютер отклоняется, а не заводится молча.
    /// Автоматическая регистрация позволяла бы любому пользователю засорять
    /// таблицу произвольными идентификаторами.
    /// </summary>
    [Fact]
    public async Task AuthorizeAsync_UnknownClient_Rejected()
    {
        var userId = await AddUserAsync("user-1");

        var result = await _service.AuthorizeAsync(userId, isAdmin: false, "нет-такого");

        Assert.False(result.IsAllowed);
        Assert.Equal(ClientAccessOutcome.UnknownClient, result.Outcome);
        Assert.NotNull(result.Reason);
    }

    /// <summary>Администратору незарегистрированный компьютер тоже недоступен.</summary>
    [Fact]
    public async Task AuthorizeAsync_UnknownClientForAdmin_Rejected()
    {
        var adminId = await AddUserAsync("admin-1");

        var result = await _service.AuthorizeAsync(adminId, isAdmin: true, "нет-такого");

        Assert.Equal(ClientAccessOutcome.UnknownClient, result.Outcome);
    }

    /// <summary>Помеченный удалённым компьютер считается несуществующим.</summary>
    [Fact]
    public async Task AuthorizeAsync_DeletedClient_Rejected()
    {
        var userId = await AddUserAsync("user-1");
        await AddClientAsync("pc-1", isActive: false);

        var result = await _service.AuthorizeAsync(userId, isAdmin: true, "pc-1");

        Assert.Equal(ClientAccessOutcome.UnknownClient, result.Outcome);
    }

    /// <summary>
    /// Заблокированный компьютер отклоняется даже у администратора.
    /// Раньше признак блокировки выставлялся, но на пути запроса не читался
    /// нигде, и заблокированный компьютер продолжал качать обновления.
    /// </summary>
    [Fact]
    public async Task AuthorizeAsync_BlockedClient_RejectedEvenForAdmin()
    {
        var adminId = await AddUserAsync("admin-1");
        await AddClientAsync("pc-1", isBlocked: true);

        var result = await _service.AuthorizeAsync(adminId, isAdmin: true, "pc-1");

        Assert.False(result.IsAllowed);
        Assert.Equal(ClientAccessOutcome.Blocked, result.Outcome);
    }

    /// <summary>Причина блокировки попадает в ответ, чтобы её увидел пользователь.</summary>
    [Fact]
    public async Task AuthorizeAsync_BlockedClient_ReturnsBlockReason()
    {
        var userId = await AddUserAsync("user-1");
        await AddClientAsync("pc-1", isBlocked: true);

        _database.Context.ClientBlockHistories.Add(new ClientBlockHistoryEntity
        {
            ClientId = "pc-1",
            Action = "blocked",
            Reason = "выведен из эксплуатации",
            CreatedAt = DateTime.UtcNow
        });
        await _database.Context.SaveChangesAsync();

        var result = await _service.AuthorizeAsync(userId, isAdmin: false, "pc-1");

        Assert.Equal(ClientAccessOutcome.Blocked, result.Outcome);
        Assert.Contains("выведен из эксплуатации", result.Reason!, StringComparison.Ordinal);
    }

    /// <summary>Администратор получает доступ к любому незаблокированному компьютеру.</summary>
    [Fact]
    public async Task AuthorizeAsync_Admin_AllowedWithoutExplicitGrant()
    {
        var adminId = await AddUserAsync("admin-1");
        await AddClientAsync("pc-1");

        var result = await _service.AuthorizeAsync(adminId, isAdmin: true, "pc-1");

        Assert.True(result.IsAllowed);
    }

    /// <summary>Пользователь без прав получает отказ.</summary>
    [Fact]
    public async Task AuthorizeAsync_UserWithoutGrant_Forbidden()
    {
        var userId = await AddUserAsync("user-1");
        await AddClientAsync("pc-1");

        var result = await _service.AuthorizeAsync(userId, isAdmin: false, "pc-1");

        Assert.False(result.IsAllowed);
        Assert.Equal(ClientAccessOutcome.Forbidden, result.Outcome);
    }

    /// <summary>Персональное разрешение открывает доступ к компьютеру.</summary>
    [Fact]
    public async Task AuthorizeAsync_DirectGrant_AllowsAccess()
    {
        var userId = await AddUserAsync("user-1");
        await AddClientAsync("pc-1");
        await GrantClientAccessAsync(userId, "pc-1");

        var result = await _service.AuthorizeAsync(userId, isAdmin: false, "pc-1");

        Assert.True(result.IsAllowed);
    }

    /// <summary>
    /// Разрешение на группу открывает доступ ко всем её компьютерам —
    /// ради этого группы и заведены.
    /// </summary>
    [Fact]
    public async Task AuthorizeAsync_GroupGrant_AllowsAccessToGroupClients()
    {
        var userId = await AddUserAsync("user-1");
        await AddGroupAsync("group-1", "Бухгалтерия");
        await AddClientAsync("pc-1", groupId: "group-1");
        await GrantGroupAccessAsync(userId, "group-1");

        var result = await _service.AuthorizeAsync(userId, isAdmin: false, "pc-1");

        Assert.True(result.IsAllowed);
    }

    /// <summary>
    /// Разрешение на одну группу не открывает компьютеры другой группы.
    /// Проверка парная к предыдущей: важно, что права не «протекают».
    /// </summary>
    [Fact]
    public async Task AuthorizeAsync_GrantForAnotherGroup_DoesNotAllowAccess()
    {
        var userId = await AddUserAsync("user-1");
        await AddGroupAsync("group-1", "Бухгалтерия");
        await AddGroupAsync("group-2", "Склад");
        await AddClientAsync("pc-1", groupId: "group-2");
        await GrantGroupAccessAsync(userId, "group-1");

        var result = await _service.AuthorizeAsync(userId, isAdmin: false, "pc-1");

        Assert.Equal(ClientAccessOutcome.Forbidden, result.Outcome);
    }

    /// <summary>Пользователь без единого разрешения не проходит общую проверку.</summary>
    [Fact]
    public async Task HasAnyAccessAsync_NoGrants_ReturnsFalse()
    {
        var userId = await AddUserAsync("user-1");

        Assert.False(await _service.HasAnyAccessAsync(userId));
    }

    /// <summary>Достаточно персонального разрешения на компьютер.</summary>
    [Fact]
    public async Task HasAnyAccessAsync_DirectGrant_ReturnsTrue()
    {
        var userId = await AddUserAsync("user-1");
        await AddClientAsync("pc-1");
        await GrantClientAccessAsync(userId, "pc-1");

        Assert.True(await _service.HasAnyAccessAsync(userId));
    }

    /// <summary>Достаточно разрешения на группу, даже если в ней пока нет компьютеров.</summary>
    [Fact]
    public async Task HasAnyAccessAsync_GroupGrant_ReturnsTrue()
    {
        var userId = await AddUserAsync("user-2");
        await AddGroupAsync("group-1", "Бухгалтерия");
        await GrantGroupAccessAsync(userId, "group-1");

        Assert.True(await _service.HasAnyAccessAsync(userId));
    }

    /// <summary>Освобождает базу.</summary>
    public void Dispose()
    {
        _database.Dispose();
        GC.SuppressFinalize(this);
    }
}
