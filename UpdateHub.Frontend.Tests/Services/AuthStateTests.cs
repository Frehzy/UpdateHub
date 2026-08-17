using System.Net;
using UpdateHub.FrontendServer.Services;
using UpdateHub.Frontend.Tests.TestSupport;

namespace UpdateHub.Frontend.Tests.Services;

/// <summary>
/// Проверяет состояние входа в панель управления.
/// </summary>
/// <remarks>
/// Здесь сходятся два решения, которые легко нарушить незаметно. Первое:
/// панель входит тем же адресом и тем же форматом, что и bash-скрипт, —
/// заводить для браузера второй способ входа значило бы держать на сервере
/// вторую проверку пароля. Второе: панель доступна только администратору,
/// и отсеивать обычного пользователя нужно сразу после входа, иначе он
/// получит отказ на каждом запросе и будет разбираться в пустых списках.
/// </remarks>
public class AuthStateTests
{
    private const string AccessTokenKey = "updatehub.access-token";
    private const string RefreshTokenKey = "updatehub.refresh-token";

    /// <summary>Собирает состояние входа поверх подставных зависимостей.</summary>
    /// <param name="handler">Подставной обработчик HTTP.</param>
    /// <param name="storage">Подставное хранилище браузера.</param>
    /// <returns>Готовое состояние входа.</returns>
    private static AuthState CreateAuthState(FakeHttpHandler handler, FakeBrowserStorage storage)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://server/") }, storage);

    /// <summary>Составляет ответ сервера на вход.</summary>
    /// <param name="role">Роль пользователя.</param>
    /// <param name="mustChangePassword">Требуется ли смена пароля.</param>
    /// <returns>Тело ответа в формате «ключ=значение».</returns>
    private static string LoginResponse(string role = "Admin", bool mustChangePassword = false)
        => $"access_token=access-1\nrefresh_token=refresh-1\nexpires_in=3600\n" +
           $"user_id=u-1\nusername=admin\nrole={role}\n" +
           $"must_change_password={(mustChangePassword ? "1" : "0")}\n";

    /// <summary>Удачный вход сохраняет токены и сведения о вошедшем.</summary>
    [Fact]
    public async Task LoginAsync_Successful_StoresTokensAndUser()
    {
        var handler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var storage = new FakeBrowserStorage();
        var auth = CreateAuthState(handler, storage);

        var error = await auth.LoginAsync("admin", "parol12345");

        Assert.Null(error);
        Assert.True(auth.IsAuthenticated);
        Assert.True(auth.IsAdmin);
        Assert.Equal("admin", auth.Username);
        Assert.Equal("access-1", auth.AccessToken);
    }

    /// <summary>
    /// Вход отправляет форму на тот же адрес, которым пользуется скрипт.
    /// </summary>
    [Fact]
    public async Task LoginAsync_SendsFormToClientEndpoint()
    {
        var handler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = CreateAuthState(handler, new FakeBrowserStorage());

        await auth.LoginAsync("admin", "parol12345");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("api/v1/auth/login", request.Url, StringComparison.Ordinal);
        Assert.Contains("username=admin", request.Body);
        Assert.Contains("password=parol12345", request.Body);
    }

    /// <summary>
    /// Токены переживают обновление страницы: они попадают в хранилище браузера.
    /// </summary>
    [Fact]
    public async Task LoginAsync_KeepsTokensInBrowserStorage()
    {
        var storage = new FakeBrowserStorage();
        var handler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = CreateAuthState(handler, storage);

        await auth.LoginAsync("admin", "parol12345");

        Assert.Equal("access-1", storage.Read(AccessTokenKey));
        Assert.Equal("refresh-1", storage.Read(RefreshTokenKey));
    }

    /// <summary>
    /// Обычный пользователь в панель не попадает, даже если пароль верен.
    /// </summary>
    /// <remarks>
    /// Сервер выдаёт ему настоящие токены — на клиентскую часть API он имеет
    /// полное право. Не пускать его именно в панель обязана панель.
    /// </remarks>
    [Fact]
    public async Task LoginAsync_OrdinaryUser_RejectedWithExplanation()
    {
        var handler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(role: "Client"), "text/plain");
        var storage = new FakeBrowserStorage();
        var auth = CreateAuthState(handler, storage);

        var error = await auth.LoginAsync("ivanov", "parol12345");

        Assert.NotNull(error);
        Assert.False(auth.IsAuthenticated);
        Assert.Null(storage.Read(AccessTokenKey));
    }

    /// <summary>Отказ сервера показывается его же словами.</summary>
    [Fact]
    public async Task LoginAsync_ServerRejects_ShowsServerMessage()
    {
        var handler = new FakeHttpHandler().Respond(
            HttpStatusCode.Unauthorized,
            "error=Неверный логин или пароль\n",
            "text/plain");

        var auth = CreateAuthState(handler, new FakeBrowserStorage());

        var error = await auth.LoginAsync("admin", "ne-tot");

        Assert.Equal("Неверный логин или пароль", error);
        Assert.False(auth.IsAuthenticated);
    }

    /// <summary>Недоступный сервер не роняет панель.</summary>
    [Fact]
    public async Task LoginAsync_ServerUnreachable_ReturnsReadableError()
    {
        var handler = new FakeHttpHandler().RespondWithNetworkFailure();
        var auth = CreateAuthState(handler, new FakeBrowserStorage());

        var error = await auth.LoginAsync("admin", "parol12345");

        Assert.Equal("Сервер недоступен", error);
    }

    /// <summary>Требование сменить пароль доходит до панели.</summary>
    [Fact]
    public async Task LoginAsync_TemporaryPassword_RaisesChangeRequirement()
    {
        var handler = new FakeHttpHandler().Respond(
            HttpStatusCode.OK,
            LoginResponse(mustChangePassword: true),
            "text/plain");

        var auth = CreateAuthState(handler, new FakeBrowserStorage());

        await auth.LoginAsync("admin", "vremennyy");

        Assert.True(auth.MustChangePassword);
    }

    /// <summary>
    /// При открытии панели состояние восстанавливается по сохранённому
    /// refresh-токену.
    /// </summary>
    /// <remarks>
    /// Именно обновлением, а не доверием к сохранённому access-токену: тот мог
    /// протухнуть, пока вкладка была закрыта, и заодно приходят свежие логин
    /// и роль.
    /// </remarks>
    [Fact]
    public async Task InitializeAsync_WithStoredToken_RestoresSession()
    {
        var storage = new FakeBrowserStorage();
        storage.Seed(RefreshTokenKey, "refresh-staryy");
        storage.Seed(AccessTokenKey, "access-staryy");

        var handler = new FakeHttpHandler().Respond(
            HttpStatusCode.OK,
            "access_token=access-2\nrefresh_token=refresh-2\nusername=admin\nrole=Admin\n",
            "text/plain");

        var auth = CreateAuthState(handler, storage);

        await auth.InitializeAsync();

        Assert.True(auth.IsReady);
        Assert.True(auth.IsAuthenticated);
        Assert.Equal("access-2", auth.AccessToken);
        Assert.Equal("refresh-2", storage.Read(RefreshTokenKey));
    }

    /// <summary>
    /// Отозванный refresh-токен приводит к очистке состояния, а не к попыткам
    /// работать по нему дальше.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WithRevokedToken_ClearsSession()
    {
        var storage = new FakeBrowserStorage();
        storage.Seed(RefreshTokenKey, "refresh-otozvannyy");

        var handler = new FakeHttpHandler().Respond(HttpStatusCode.Unauthorized, "error=Токен отозван\n", "text/plain");
        var auth = CreateAuthState(handler, storage);

        await auth.InitializeAsync();

        Assert.True(auth.IsReady);
        Assert.False(auth.IsAuthenticated);
        Assert.Null(storage.Read(RefreshTokenKey));
    }

    /// <summary>Без сохранённого токена панель просто готова к входу.</summary>
    [Fact]
    public async Task InitializeAsync_WithoutStoredToken_ReadyAndSignedOut()
    {
        var auth = CreateAuthState(new FakeHttpHandler(), new FakeBrowserStorage());

        await auth.InitializeAsync();

        Assert.True(auth.IsReady);
        Assert.False(auth.IsAuthenticated);
    }

    /// <summary>
    /// Запрет хранилища в браузере не мешает работать.
    /// </summary>
    /// <remarks>
    /// Вход при этом не переживает обновления страницы, но панель обязана
    /// открыться и дать войти, а не показать пустой экран.
    /// </remarks>
    [Fact]
    public async Task LoginAsync_StorageBlockedByBrowser_StillSignsIn()
    {
        var storage = new FakeBrowserStorage { IsBlocked = true };
        var handler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = CreateAuthState(handler, storage);

        var error = await auth.LoginAsync("admin", "parol12345");

        Assert.Null(error);
        Assert.True(auth.IsAuthenticated);
    }

    /// <summary>Выход отзывает токен на сервере и очищает хранилище.</summary>
    [Fact]
    public async Task LogoutAsync_RevokesTokenAndClearsStorage()
    {
        var storage = new FakeBrowserStorage();
        var handler = new FakeHttpHandler()
            .Respond(HttpStatusCode.OK, LoginResponse(), "text/plain")
            .Respond(HttpStatusCode.NoContent);

        var auth = CreateAuthState(handler, storage);
        await auth.LoginAsync("admin", "parol12345");

        await auth.LogoutAsync();

        Assert.False(auth.IsAuthenticated);
        Assert.Null(storage.Read(AccessTokenKey));
        Assert.Null(storage.Read(RefreshTokenKey));

        var logout = handler.Requests[^1];
        Assert.EndsWith("api/v1/auth/logout", logout.Url, StringComparison.Ordinal);
        Assert.Equal("access-1", logout.BearerToken);
    }

    /// <summary>
    /// Недоступный сервер не оставляет панель «наполовину вошедшей».
    /// </summary>
    [Fact]
    public async Task LogoutAsync_ServerUnreachable_StillClearsSession()
    {
        var storage = new FakeBrowserStorage();
        var handler = new FakeHttpHandler()
            .Respond(HttpStatusCode.OK, LoginResponse(), "text/plain")
            .RespondWithNetworkFailure();

        var auth = CreateAuthState(handler, storage);
        await auth.LoginAsync("admin", "parol12345");

        await auth.LogoutAsync();

        Assert.False(auth.IsAuthenticated);
        Assert.Null(storage.Read(RefreshTokenKey));
    }

    /// <summary>Изменение состояния входа доходит до подписчиков.</summary>
    /// <remarks>
    /// На этом событии держится перерисовка: раскладка панели решает по нему,
    /// показывать форму входа или разделы.
    /// </remarks>
    [Fact]
    public async Task LoginAsync_NotifiesSubscribers()
    {
        var handler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = CreateAuthState(handler, new FakeBrowserStorage());

        var notifications = 0;
        auth.Changed += () => notifications++;

        await auth.LoginAsync("admin", "parol12345");

        Assert.True(notifications > 0);
    }

    /// <summary>Отметка о смене пароля снимает требование.</summary>
    [Fact]
    public void MarkPasswordChanged_ClearsRequirement()
    {
        var auth = CreateAuthState(new FakeHttpHandler(), new FakeBrowserStorage());
        var notified = false;
        auth.Changed += () => notified = true;

        auth.MarkPasswordChanged();

        Assert.False(auth.MustChangePassword);
        Assert.True(notified);
    }
}
