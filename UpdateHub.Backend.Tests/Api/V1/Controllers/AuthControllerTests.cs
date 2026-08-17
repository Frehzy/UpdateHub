using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Net;
using UpdateHub.Backend.Tests.TestSupport;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Backend.Tests.Api.V1.Controllers;

/// <summary>
/// Проверяет вход, обновление и отзыв токенов через настоящий HTTP.
/// </summary>
/// <remarks>
/// Служба авторизации проверена отдельно; здесь важно другое — что запрос
/// доходит до неё в неизменном виде. Поля формы называются в скрипте
/// <c>username</c>, <c>password</c>, <c>client_id</c>, а свойства класса —
/// <c>Username</c>, <c>Password</c>, <c>ClientId</c>. Связывает их атрибут
/// <c>FromForm(Name = ...)</c>, и опечатка в нём не видна ни компилятору,
/// ни модульным тестам: сервер просто получит пустой логин.
/// <para>
/// Ответ разбирается как «ключ=значение» — в том же виде, в каком его читает
/// bash-скрипт через <c>grep</c> и <c>cut</c>.
/// </para>
/// </remarks>
[Collection(ApiCollection.Name)]
public class AuthControllerTests(UpdateHubApplication application)
{
    /// <summary>Отправляет форму на указанный адрес.</summary>
    /// <param name="client">Клиент.</param>
    /// <param name="path">Адрес.</param>
    /// <param name="fields">Поля формы.</param>
    /// <returns>Ответ сервера.</returns>
    private static Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string path,
        params (string Key, string Value)[] fields)
        => client.PostAsync(path, new FormUrlEncodedContent(
            fields.Select(field => new KeyValuePair<string, string>(field.Key, field.Value))));

    /// <summary>
    /// Вход администратора выдаёт оба токена, роль и признак обязательной
    /// смены пароля.
    /// </summary>
    /// <remarks>
    /// Признак важен: первый администратор заводится с временным паролем,
    /// и панель управления обязана потребовать его смену.
    /// </remarks>
    [Fact]
    public async Task Login_Administrator_ReturnsTokensAndRole()
    {
        using var client = application.CreateApiClient();

        var response = await PostFormAsync(
            client,
            "/api/v1/auth/login",
            ("username", UpdateHubApplication.AdminUsername),
            ("password", UpdateHubApplication.AdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var pairs = UpdateHubApplication.ParseTextPairs(await response.Content.ReadAsStringAsync());

        Assert.False(string.IsNullOrEmpty(pairs["access_token"]));
        Assert.False(string.IsNullOrEmpty(pairs["refresh_token"]));
        Assert.Equal(nameof(UserRole.Admin), pairs["role"]);
        Assert.Equal("1", pairs["must_change_password"]);
    }

    /// <summary>Ответ на вход — текст, а не JSON: скрипт разбирает его без jq.</summary>
    [Fact]
    public async Task Login_AnswersWithPlainText()
    {
        using var client = application.CreateApiClient();

        var response = await PostFormAsync(
            client,
            "/api/v1/auth/login",
            ("username", UpdateHubApplication.AdminUsername),
            ("password", UpdateHubApplication.AdminPassword));

        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("{", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>Неверный пароль — 401 и сообщение текстом.</summary>
    [Fact]
    public async Task Login_WrongPassword_Unauthorized()
    {
        using var client = application.CreateApiClient();

        var response = await PostFormAsync(
            client,
            "/api/v1/auth/login",
            ("username", UpdateHubApplication.AdminUsername),
            ("password", "sovsem-ne-tot-parol"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.StartsWith("error=", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// <summary>Несуществующий пользователь неотличим от неверного пароля.</summary>
    /// <remarks>
    /// Различие в ответах позволило бы перебором узнать, какие логины заведены.
    /// </remarks>
    [Fact]
    public async Task Login_UnknownUser_AnswersLikeWrongPassword()
    {
        using var client = application.CreateApiClient();

        var response = await PostFormAsync(
            client,
            "/api/v1/auth/login",
            ("username", "takogo-polzovatelya-net"),
            ("password", "lyuboy-parol"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Форма без пароля отклоняется проверкой, а не доходит до службы.</summary>
    [Fact]
    public async Task Login_WithoutPassword_BadRequest()
    {
        using var client = application.CreateApiClient();

        var response = await PostFormAsync(client, "/api/v1/auth/login", ("username", "ivanov"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Обычный пользователь без выданных прав войти не может: работать ему
    /// всё равно не с чем, а внятный отказ лучше пустого списка файлов.
    /// </summary>
    [Fact]
    public async Task Login_UserWithoutGrants_Unauthorized()
    {
        await application.AddUserAsync("bez-prav", "parol12345");
        using var client = application.CreateApiClient();

        var response = await PostFormAsync(
            client,
            "/api/v1/auth/login",
            ("username", "bez-prav"),
            ("password", "parol12345"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Вход с указанием компьютера возвращает его идентификатор.</summary>
    [Fact]
    public async Task Login_WithClient_ReturnsClientIdentifier()
    {
        var user = await application.AddUserAsync("s-pravami", "parol12345");
        await application.AddClientAsync("pc-vhod", user.Id);

        using var client = application.CreateApiClient();

        var response = await PostFormAsync(
            client,
            "/api/v1/auth/login",
            ("username", "s-pravami"),
            ("password", "parol12345"),
            ("client_id", "pc-vhod"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var pairs = UpdateHubApplication.ParseTextPairs(await response.Content.ReadAsStringAsync());
        Assert.Equal("pc-vhod", pairs["client_id"]);
    }

    /// <summary>
    /// Сведения о компьютере, переданные при входе, сохраняются: по ним
    /// администратор видит, что за машина обращалась.
    /// </summary>
    [Fact]
    public async Task Login_WithHardwareDetails_StoresComputerInfo()
    {
        var user = await application.AddUserAsync("s-zhelezom", "parol12345");
        await application.AddClientAsync("pc-zhelezo", user.Id);

        using var client = application.CreateApiClient();

        var response = await PostFormAsync(
            client,
            "/api/v1/auth/login",
            ("username", "s-zhelezom"),
            ("password", "parol12345"),
            ("client_id", "pc-zhelezo"),
            ("hostname", "buhgalteriya-01"),
            ("os_version", "Astra Linux 1.7.6"),
            ("memory_gb", "2"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await application.WithDatabaseAsync(async context =>
        {
            var info = await context.ClientComputerInfos
                .FirstOrDefaultAsync(x => x.ClientId == "pc-zhelezo");

            Assert.NotNull(info);
            Assert.Equal("buhgalteriya-01", info.Hostname);
            Assert.Equal("Astra Linux 1.7.6", info.OsVersion);
            Assert.Equal(2, info.MemoryGb);
        });
    }

    /// <summary>Обновление по refresh-токену выдаёт новый access-токен.</summary>
    [Fact]
    public async Task Refresh_ValidToken_IssuesNewTokens()
    {
        using var client = application.CreateApiClient();

        var login = await PostFormAsync(
            client,
            "/api/v1/auth/login",
            ("username", UpdateHubApplication.AdminUsername),
            ("password", UpdateHubApplication.AdminPassword));

        var tokens = UpdateHubApplication.ParseTextPairs(await login.Content.ReadAsStringAsync());

        var response = await PostFormAsync(
            client,
            "/api/v1/auth/refresh",
            ("refresh_token", tokens["refresh_token"]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refreshed = UpdateHubApplication.ParseTextPairs(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrEmpty(refreshed["access_token"]));
        Assert.NotEqual(tokens["refresh_token"], refreshed["refresh_token"]);
    }

    /// <summary>Выдуманный refresh-токен отклоняется.</summary>
    [Fact]
    public async Task Refresh_UnknownToken_Unauthorized()
    {
        using var client = application.CreateApiClient();

        var response = await PostFormAsync(client, "/api/v1/auth/refresh", ("refresh_token", "vydumannyy-token"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// После выхода прежний refresh-токен перестаёт работать.
    /// </summary>
    /// <remarks>
    /// Проверка не формальная: отзыв выполняется запросом на обновление
    /// записи, минуя отслеживание изменений. Прежняя версия после отзыва
    /// читала токен из кэша контекста и считала его действующим.
    /// </remarks>
    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var user = await application.AddUserAsync("vyhod", "parol12345");
        await application.AddClientAsync("pc-vyhod", user.Id);

        using var client = application.CreateApiClient();

        var login = await PostFormAsync(
            client,
            "/api/v1/auth/login",
            ("username", "vyhod"),
            ("password", "parol12345"));

        var tokens = UpdateHubApplication.ParseTextPairs(await login.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens["access_token"]);

        var logout = await PostFormAsync(client, "/api/v1/auth/logout", ("refresh_token", tokens["refresh_token"]));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var refresh = await PostFormAsync(client, "/api/v1/auth/refresh", ("refresh_token", tokens["refresh_token"]));

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    /// <summary>Выход без токена доступа не выполняется.</summary>
    [Fact]
    public async Task Logout_WithoutToken_Unauthorized()
    {
        using var client = application.CreateApiClient();

        var response = await PostFormAsync(client, "/api/v1/auth/logout", ("refresh_token", "chto-ugodno"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Смена пароля требует правильного текущего пароля.</summary>
    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Unauthorized()
    {
        var user = await application.AddUserAsync("smena-otkaz", "parol12345");
        await application.AddClientAsync("pc-smena-otkaz", user.Id);

        using var client = await application.CreateAuthorizedClientAsync("smena-otkaz", "parol12345");

        var response = await PostFormAsync(
            client,
            "/api/v1/auth/change-password",
            ("current_password", "ne-tot-parol"),
            ("new_password", "novyy-parol-12345"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Слишком короткий новый пароль отклоняется проверкой формы.</summary>
    [Fact]
    public async Task ChangePassword_TooShortNewPassword_BadRequest()
    {
        var user = await application.AddUserAsync("smena-korotkiy", "parol12345");
        await application.AddClientAsync("pc-smena-korotkiy", user.Id);

        using var client = await application.CreateAuthorizedClientAsync("smena-korotkiy", "parol12345");

        var response = await PostFormAsync(
            client,
            "/api/v1/auth/change-password",
            ("current_password", "parol12345"),
            ("new_password", "korotko"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// После смены пароля вход выполняется новым паролем, а старый
    /// перестаёт действовать.
    /// </summary>
    [Fact]
    public async Task ChangePassword_ThenLoginWithNewPassword()
    {
        var user = await application.AddUserAsync("smena-uspeh", "parol12345");
        await application.AddClientAsync("pc-smena-uspeh", user.Id);

        using var authorized = await application.CreateAuthorizedClientAsync("smena-uspeh", "parol12345");

        var change = await PostFormAsync(
            authorized,
            "/api/v1/auth/change-password",
            ("current_password", "parol12345"),
            ("new_password", "novyy-parol-12345"));

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);

        using var client = application.CreateApiClient();

        var withOld = await PostFormAsync(
            client,
            "/api/v1/auth/login",
            ("username", "smena-uspeh"),
            ("password", "parol12345"));

        var withNew = await PostFormAsync(
            client,
            "/api/v1/auth/login",
            ("username", "smena-uspeh"),
            ("password", "novyy-parol-12345"));

        Assert.Equal(HttpStatusCode.Unauthorized, withOld.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withNew.StatusCode);
    }
}
