using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UpdateHub.Server.Application.Repositories;
using UpdateHub.Server.Application.Services;
using UpdateHub.Server.Application.Sync;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Domain.Enums;
using UpdateHub.Server.Infrastructure.Security;
using UpdateHub.Tests.TestSupport;

namespace UpdateHub.Tests.Application.Services;

/// <summary>
/// Проверяет вход в систему и работу с токенами.
/// </summary>
/// <remarks>
/// Отдельного внимания заслуживает вход без указания компьютера. Он появился
/// как исправление тупика: проверка компьютера выполнялась всегда, а на чистой
/// базе компьютеров нет, поэтому созданный при первом запуске администратор
/// не мог войти — и завести первый компьютер было некому.
/// </remarks>
public class AuthServiceTests : IDisposable
{
    /// <summary>Ключ длиной не меньше 32 байт.</summary>
    private const string TestSecret = "kluch-dlya-testov-dostatochno-dlinnyy-1234567890";

    private readonly TestDatabase _database;
    private readonly PasswordHasher _hasher = new(workFactor: 4);
    private readonly FakeClientService _clientService = new();
    private readonly AuthService _service;
    private readonly ConnectionContext _connection = new("192.168.1.10", "curl/8.0");

    /// <summary>Готовит базу и службу авторизации.</summary>
    public AuthServiceTests()
    {
        _database = new TestDatabase();
        var context = _database.Context;

        var tokenGenerator = new TokenGenerator(Options.Create(new JwtSettings
        {
            Issuer = "UpdateHub",
            Audience = "UpdateClients",
            SecretKey = TestSecret,
            AccessTokenExpiryMinutes = 60,
            RefreshTokenExpiryDays = 7
        }));

        var accessService = new ClientAccessService(
            new ClientRepository(context),
            new UserClientAccessRepository(context),
            new UserGroupAccessRepository(context),
            new ClientBlockHistoryRepository(context),
            NullLogger<ClientAccessService>.Instance);

        _service = new AuthService(
            new UserRepository(context),
            new RefreshTokenRepository(context),
            new UserClientAccessRepository(context),
            new UserGroupAccessRepository(context),
            accessService,
            _clientService,
            tokenGenerator,
            _hasher,
            NullLogger<AuthService>.Instance);
    }

    /// <summary>Заводит пользователя.</summary>
    /// <param name="username">Логин.</param>
    /// <param name="password">Пароль.</param>
    /// <param name="role">Роль.</param>
    /// <param name="isActive">Признак активности.</param>
    /// <returns>Созданный пользователь.</returns>
    private async Task<UserEntity> AddUserAsync(
        string username = "ivanov",
        string password = "parol12345",
        UserRole role = UserRole.Client,
        bool isActive = true)
    {
        var user = new UserEntity
        {
            Username = username,
            PasswordHash = _hasher.HashPassword(password),
            Role = role,
            IsActive = isActive
        };

        _database.Context.Users.Add(user);
        await _database.Context.SaveChangesAsync();
        return user;
    }

    /// <summary>Заводит компьютер и при необходимости выдаёт на него права.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="userId">Пользователь, которому выдать права.</param>
    /// <param name="isBlocked">Признак блокировки.</param>
    private async Task AddClientAsync(string clientId, string? userId = null, bool isBlocked = false)
    {
        _database.Context.Clients.Add(new ClientEntity { Id = clientId, IsActive = true, IsBlocked = isBlocked });

        if (userId is not null)
        {
            _database.Context.UserClientAccesses.Add(new UserClientAccessEntity { UserId = userId, ClientId = clientId });
        }

        await _database.Context.SaveChangesAsync();
    }

    /// <summary>
    /// Администратор входит на чистой базе, где нет ни одного компьютера.
    /// Без этого систему невозможно ввести в эксплуатацию.
    /// </summary>
    [Fact]
    public async Task LoginAsync_АдминистраторНаЧистойБазе_ВходитБезКомпьютера()
    {
        await AddUserAsync("admin", "adminparol", UserRole.Admin);

        var result = await _service.LoginAsync("admin", "adminparol", string.Empty, _connection);

        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.Equal("Admin", result.Role);
        Assert.Null(result.ClientId);
    }

    /// <summary>
    /// Обычному пользователю без единого разрешения токен не выдаётся:
    /// пользоваться им всё равно негде.
    /// </summary>
    [Fact]
    public async Task LoginAsync_ПользовательБезПравБезКомпьютера_ПолучаетОтказ()
    {
        await AddUserAsync();

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.LoginAsync("ivanov", "parol12345", string.Empty, _connection));
    }

    /// <summary>Пользователь с правами хотя бы на один компьютер входит в панель управления.</summary>
    [Fact]
    public async Task LoginAsync_ПользовательСПравамиБезКомпьютера_Входит()
    {
        var user = await AddUserAsync();
        await AddClientAsync("pc-1", user.Id);

        var result = await _service.LoginAsync("ivanov", "parol12345", string.Empty, _connection);

        Assert.False(string.IsNullOrEmpty(result.AccessToken));
        Assert.Null(result.ClientId);
    }

    /// <summary>Вход с указанием компьютера привязывает токен к нему.</summary>
    [Fact]
    public async Task LoginAsync_СКомпьютером_ТокенПривязанКНему()
    {
        var user = await AddUserAsync();
        await AddClientAsync("pc-1", user.Id);

        var result = await _service.LoginAsync("ivanov", "parol12345", "pc-1", _connection);

        Assert.Equal("pc-1", result.ClientId);
        Assert.Contains(_clientService.History, h => h.ChangeType == ClientChangeType.LoggedIn);
    }

    /// <summary>Неверный пароль отклоняется.</summary>
    [Fact]
    public async Task LoginAsync_НеверныйПароль_Отклоняется()
    {
        await AddUserAsync("admin", "adminparol", UserRole.Admin);

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.LoginAsync("admin", "не-тот-пароль", string.Empty, _connection));
    }

    /// <summary>Неизвестный логин отклоняется тем же исключением, что и неверный пароль.</summary>
    [Fact]
    public async Task LoginAsync_НеизвестныйЛогин_Отклоняется()
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.LoginAsync("нет-такого", "любой", string.Empty, _connection));
    }

    /// <summary>Отключённая учётная запись войти не может.</summary>
    [Fact]
    public async Task LoginAsync_ОтключённаяУчётнаяЗапись_Отклоняется()
    {
        await AddUserAsync("admin", "adminparol", UserRole.Admin, isActive: false);

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.LoginAsync("admin", "adminparol", string.Empty, _connection));
    }

    /// <summary>
    /// Незарегистрированный компьютер отклоняется и не появляется в базе.
    /// Раньше запись создавалась до проверки прав и оставалась даже при отказе.
    /// </summary>
    [Fact]
    public async Task LoginAsync_НеизвестныйКомпьютер_ОтклоняетсяИНеСоздаётЗапись()
    {
        await AddUserAsync("admin", "adminparol", UserRole.Admin);

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.LoginAsync("admin", "adminparol", "выдуманный-компьютер", _connection));

        using var context = _database.CreateSeparateContext();
        Assert.Empty(context.Clients);
    }

    /// <summary>Вход с заблокированного компьютера отклоняется.</summary>
    [Fact]
    public async Task LoginAsync_ЗаблокированныйКомпьютер_Отклоняется()
    {
        var user = await AddUserAsync();
        await AddClientAsync("pc-1", user.Id, isBlocked: true);

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.LoginAsync("ivanov", "parol12345", "pc-1", _connection));
    }

    /// <summary>При входе сохраняется хэш refresh-токена, а не сам токен.</summary>
    [Fact]
    public async Task LoginAsync_СохраняетХэшТокенаАНеСамТокен()
    {
        await AddUserAsync("admin", "adminparol", UserRole.Admin);

        var result = await _service.LoginAsync("admin", "adminparol", string.Empty, _connection);

        using var context = _database.CreateSeparateContext();
        var stored = context.RefreshTokens.Single();

        Assert.NotEqual(result.RefreshToken, stored.Token);
        Assert.Equal("192.168.1.10", stored.ClientIp);
    }

    /// <summary>Обновление выдаёт новую пару токенов.</summary>
    [Fact]
    public async Task RefreshAsync_ДействующийТокен_ВыдаётНовуюПару()
    {
        await AddUserAsync("admin", "adminparol", UserRole.Admin);
        var login = await _service.LoginAsync("admin", "adminparol", string.Empty, _connection);

        var refreshed = await _service.RefreshAsync(login.RefreshToken, _connection);

        Assert.False(string.IsNullOrEmpty(refreshed.AccessToken));
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
    }

    /// <summary>
    /// Прежний refresh-токен после обновления перестаёт действовать.
    /// Ротация не позволяет пользоваться перехваченным значением после того,
    /// как им воспользовался законный владелец.
    /// </summary>
    [Fact]
    public async Task RefreshAsync_ПрежнийТокен_ПерестаётДействовать()
    {
        await AddUserAsync("admin", "adminparol", UserRole.Admin);
        var login = await _service.LoginAsync("admin", "adminparol", string.Empty, _connection);

        await _service.RefreshAsync(login.RefreshToken, _connection);

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.RefreshAsync(login.RefreshToken, _connection));
    }

    /// <summary>Неизвестный refresh-токен отклоняется.</summary>
    [Fact]
    public async Task RefreshAsync_НеизвестныйТокен_Отклоняется()
    {
        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.RefreshAsync("выдуманный-токен", _connection));
    }

    /// <summary>После выхода токен перестаёт действовать.</summary>
    [Fact]
    public async Task LogoutAsync_ОтзываетТокен()
    {
        var user = await AddUserAsync("admin", "adminparol", UserRole.Admin);
        var login = await _service.LoginAsync("admin", "adminparol", string.Empty, _connection);

        await _service.LogoutAsync(login.RefreshToken, user.Id);

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.RefreshAsync(login.RefreshToken, _connection));
    }

    /// <summary>Чужой токен отозвать нельзя.</summary>
    [Fact]
    public async Task LogoutAsync_ЧужойТокен_НеОтзывается()
    {
        await AddUserAsync("admin", "adminparol", UserRole.Admin);
        var login = await _service.LoginAsync("admin", "adminparol", string.Empty, _connection);

        await _service.LogoutAsync(login.RefreshToken, "другой-пользователь");

        var refreshed = await _service.RefreshAsync(login.RefreshToken, _connection);
        Assert.False(string.IsNullOrEmpty(refreshed.AccessToken));
    }

    /// <summary>Смена пароля меняет пароль и снимает требование его сменить.</summary>
    [Fact]
    public async Task ChangePasswordAsync_МеняетПарольИСнимаетТребование()
    {
        var user = await AddUserAsync("admin", "adminparol", UserRole.Admin);
        user.MustChangePassword = true;
        await _database.Context.SaveChangesAsync();

        await _service.ChangePasswordAsync(user.Id, "adminparol", "novyyparol123");

        using var context = _database.CreateSeparateContext();
        var updated = context.Users.Single();

        Assert.True(_hasher.VerifyPassword("novyyparol123", updated.PasswordHash));
        Assert.False(updated.MustChangePassword);
    }

    /// <summary>
    /// Смена пароля отзывает все выданные пользователю refresh-токены:
    /// иначе прежний токен продолжал бы действовать со старым паролем.
    /// </summary>
    [Fact]
    public async Task ChangePasswordAsync_ОтзываетВыданныеТокены()
    {
        var user = await AddUserAsync("admin", "adminparol", UserRole.Admin);
        var login = await _service.LoginAsync("admin", "adminparol", string.Empty, _connection);

        await _service.ChangePasswordAsync(user.Id, "adminparol", "novyyparol123");

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.RefreshAsync(login.RefreshToken, _connection));
    }

    /// <summary>Неверный текущий пароль не позволяет сменить пароль.</summary>
    [Fact]
    public async Task ChangePasswordAsync_НеверныйТекущийПароль_Отклоняется()
    {
        var user = await AddUserAsync("admin", "adminparol", UserRole.Admin);

        await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => _service.ChangePasswordAsync(user.Id, "не-тот", "novyyparol123"));
    }

    /// <summary>Слишком короткий новый пароль отклоняется.</summary>
    [Fact]
    public async Task ChangePasswordAsync_КороткийНовыйПароль_Отклоняется()
    {
        var user = await AddUserAsync("admin", "adminparol", UserRole.Admin);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ChangePasswordAsync(user.Id, "adminparol", "korotk"));
    }

    /// <summary>Новый пароль обязан отличаться от текущего.</summary>
    [Fact]
    public async Task ChangePasswordAsync_ТотЖеПароль_Отклоняется()
    {
        var user = await AddUserAsync("admin", "adminparol", UserRole.Admin);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.ChangePasswordAsync(user.Id, "adminparol", "adminparol"));
    }

    /// <summary>Новому пользователю выставляется требование сменить пароль.</summary>
    [Fact]
    public async Task CreateUserAsync_НовыйПользователь_ОбязанСменитьПароль()
    {
        var user = await _service.CreateUserAsync("petrov", "parol12345", UserRole.Client, null, null);

        Assert.True(user.MustChangePassword);
        Assert.True(user.IsActive);
    }

    /// <summary>Занятый логин отклоняется.</summary>
    [Fact]
    public async Task CreateUserAsync_ЗанятыйЛогин_Отклоняется()
    {
        await AddUserAsync("petrov");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateUserAsync("petrov", "parol12345", UserRole.Client, null, null));
    }

    /// <summary>Права, указанные при создании, сразу записываются в базу.</summary>
    [Fact]
    public async Task CreateUserAsync_ВыдаётУказанныеПрава()
    {
        await AddClientAsync("pc-1");
        _database.Context.Groups.Add(new GroupEntity { Id = "group-1", Name = "Бухгалтерия" });
        await _database.Context.SaveChangesAsync();

        var user = await _service.CreateUserAsync(
            "petrov", "parol12345", UserRole.Client, ["group-1"], ["pc-1"]);

        using var context = _database.CreateSeparateContext();
        Assert.Single(context.UserClientAccesses.Where(a => a.UserId == user.Id));
        Assert.Single(context.UserGroupAccesses.Where(a => a.UserId == user.Id));
    }

    /// <summary>Короткий пароль при создании отклоняется.</summary>
    [Fact]
    public async Task CreateUserAsync_КороткийПароль_Отклоняется()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateUserAsync("petrov", "korotk", UserRole.Client, null, null));
    }

    /// <summary>Освобождает базу.</summary>
    public void Dispose() => _database.Dispose();
}
