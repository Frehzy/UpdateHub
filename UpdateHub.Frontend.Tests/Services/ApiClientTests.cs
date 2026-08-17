using System.Net;
using UpdateHub.Frontend.Tests.TestSupport;
using UpdateHub.Shared.Contracts.Groups;
using UpdateHub.Shared.Contracts.Users;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Frontend.Tests.Services;

/// <summary>
/// Проверяет обращения панели к серверу.
/// </summary>
/// <remarks>
/// Две вещи здесь важнее прочих. Первая — обновление протухшего access-токена:
/// живёт он недолго, и без повтора запроса администратор получал бы «требуется
/// вход» посреди работы. Вторая — превращение отказа в понятную человеку
/// строку: сервер отвечает то JSON с полем error, то текстом от слоя
/// авторизации, то описанием проверки формы, и разбирать это на каждой
/// странице заново значило бы однажды показать пустой экран вместо причины.
/// </remarks>
public class ApiClientTests
{
    /// <summary>Собирает панельный клиент поверх подставных зависимостей.</summary>
    /// <param name="handler">Подставной обработчик HTTP.</param>
    /// <param name="auth">Состояние входа.</param>
    /// <returns>Готовый клиент.</returns>
    private static ApiClient CreateApiClient(FakeHttpHandler handler, AuthState auth)
        => new(new HttpClient(handler) { BaseAddress = new Uri("http://server/") }, auth);

    /// <summary>Выполняет вход, чтобы у состояния появился токен.</summary>
    /// <param name="handler">Подставной обработчик HTTP.</param>
    /// <param name="storage">Подставное хранилище браузера.</param>
    /// <returns>Состояние входа с действующим токеном.</returns>
    /// <remarks>
    /// Вход выполняется отдельным обработчиком: запросы панели считаются
    /// с чистого листа, и служебный вход не сбивает нумерацию в проверках.
    /// </remarks>
    private static async Task<AuthState> CreateSignedInStateAsync(
        FakeHttpHandler handler,
        FakeBrowserStorage? storage = null)
    {
        var auth = new AuthState(
            new HttpClient(handler) { BaseAddress = new Uri("http://server/") },
            storage ?? new FakeBrowserStorage());

        await auth.LoginAsync("admin", "parol12345");
        return auth;
    }

    /// <summary>Составляет ответ сервера на вход.</summary>
    /// <param name="accessToken">Выдаваемый access-токен.</param>
    /// <returns>Тело ответа.</returns>
    private static string LoginResponse(string accessToken = "access-1")
        => $"access_token={accessToken}\nrefresh_token=refresh-1\nusername=admin\nrole=Admin\n" +
           "must_change_password=0\n";

    /// <summary>Данные разбираются в тип из общей библиотеки.</summary>
    [Fact]
    public async Task GetAsync_ParsesSharedContract()
    {
        var authHandler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = await CreateSignedInStateAsync(authHandler);

        var handler = new FakeHttpHandler().Respond(
            HttpStatusCode.OK,
            """{"groups":[{"id":"g-1","name":"Бухгалтерия","clientCount":3}],"total":1}""");

        var api = CreateApiClient(handler, auth);

        var result = await api.GetAsync<GroupListResponseDto>("api/v1/admin/groups");

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Total);
        Assert.Equal("Бухгалтерия", result.Value.Groups[0].Name);
        Assert.Equal(3, result.Value.Groups[0].ClientCount);
    }

    /// <summary>К каждому запросу подставляется заголовок авторизации.</summary>
    [Fact]
    public async Task GetAsync_AddsAuthorizationHeader()
    {
        var authHandler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = await CreateSignedInStateAsync(authHandler);

        var handler = new FakeHttpHandler().Respond(HttpStatusCode.OK, """{"groups":[],"total":0}""");
        var api = CreateApiClient(handler, auth);

        await api.GetAsync<GroupListResponseDto>("api/v1/admin/groups");

        Assert.Equal("access-1", Assert.Single(handler.Requests).BearerToken);
    }

    /// <summary>
    /// Протухший токен обновляется, и запрос повторяется — незаметно
    /// для администратора.
    /// </summary>
    /// <remarks>
    /// Обновление уходит на клиент состояния входа, а повтор — на клиент
    /// панели, поэтому в подставном обработчике панели ровно два запроса:
    /// неудачный и удачный.
    /// </remarks>
    [Fact]
    public async Task GetAsync_ExpiredToken_RefreshesAndRetries()
    {
        var authHandler = new FakeHttpHandler()
            .Respond(HttpStatusCode.OK, LoginResponse(), "text/plain")
            .Respond(HttpStatusCode.OK, LoginResponse(accessToken: "access-2"), "text/plain");

        var auth = await CreateSignedInStateAsync(authHandler);

        var handler = new FakeHttpHandler()
            .Respond(HttpStatusCode.Unauthorized, "error=Требуется действующий access-токен\n", "text/plain")
            .Respond(HttpStatusCode.OK, """{"groups":[],"total":0}""");

        var api = CreateApiClient(handler, auth);

        var result = await api.GetAsync<GroupListResponseDto>("api/v1/admin/groups");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("access-1", handler.Requests[0].BearerToken);
        Assert.Equal("access-2", handler.Requests[1].BearerToken);
    }

    /// <summary>
    /// Если и после обновления сервер отвечает 401, повтор не выполняется.
    /// </summary>
    /// <remarks>
    /// Дело уже не в сроке жизни токена, и второй заход ничего не изменит —
    /// зато превратил бы отказ в бесконечный цикл запросов.
    /// </remarks>
    [Fact]
    public async Task GetAsync_StillUnauthorizedAfterRefresh_DoesNotRetryAgain()
    {
        var authHandler = new FakeHttpHandler()
            .Respond(HttpStatusCode.OK, LoginResponse(), "text/plain")
            .Respond(HttpStatusCode.OK, LoginResponse(accessToken: "access-2"), "text/plain");

        var auth = await CreateSignedInStateAsync(authHandler);

        var handler = new FakeHttpHandler()
            .Respond(HttpStatusCode.Unauthorized, "error=Требуется действующий access-токен\n", "text/plain")
            .Respond(HttpStatusCode.Unauthorized, "error=Требуется действующий access-токен\n", "text/plain");

        var api = CreateApiClient(handler, auth);

        var result = await api.GetAsync<GroupListResponseDto>("api/v1/admin/groups");

        Assert.False(result.IsSuccess);
        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>Сообщение об ошибке берётся из ответа сервера.</summary>
    [Fact]
    public async Task GetAsync_ServerError_UsesServerMessage()
    {
        var authHandler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = await CreateSignedInStateAsync(authHandler);

        var handler = new FakeHttpHandler().Respond(
            HttpStatusCode.NotFound,
            """{"error":"Пользователь 'u-9' не найден"}""");

        var api = CreateApiClient(handler, auth);

        var result = await api.GetAsync<UserResponseDto>("api/v1/admin/users/u-9");

        Assert.False(result.IsSuccess);
        Assert.Equal("Пользователь 'u-9' не найден", result.Error);
    }

    /// <summary>
    /// Текстовый отказ слоя авторизации тоже превращается в понятную строку.
    /// </summary>
    /// <remarks>
    /// Отказ по роли приходит не JSON, а текстом: тот же обработчик отвечает
    /// и bash-скрипту, которому JSON разбирать нечем.
    /// </remarks>
    [Fact]
    public async Task GetAsync_PlainTextRejection_ParsedIntoMessage()
    {
        var authHandler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = await CreateSignedInStateAsync(authHandler);

        var handler = new FakeHttpHandler().Respond(
            HttpStatusCode.Forbidden,
            "error=Недостаточно прав для этой операции\n",
            "text/plain");

        var api = CreateApiClient(handler, auth);

        var result = await api.GetAsync<UserListResponseDto>("api/v1/admin/users");

        Assert.Equal("Недостаточно прав для этой операции", result.Error);
    }

    /// <summary>
    /// Ответ без разбираемого сообщения всё равно объясняется человеку.
    /// </summary>
    /// <remarks>
    /// Так отвечает проверка формы в ASP.NET Core: своё описание в другом
    /// формате. Показывать администратору голый код состояния бесполезно,
    /// поэтому коды переведены на русский.
    /// </remarks>
    [Fact]
    public async Task GetAsync_UnknownErrorFormat_FallsBackToStatusDescription()
    {
        var authHandler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = await CreateSignedInStateAsync(authHandler);

        var handler = new FakeHttpHandler().Respond(
            HttpStatusCode.BadRequest,
            """{"title":"One or more validation errors occurred.","status":400}""");

        var api = CreateApiClient(handler, auth);

        var result = await api.PostAsync("api/v1/admin/groups", new CreateGroupRequestDto());

        Assert.False(result.IsSuccess);
        Assert.Equal("Проверьте заполнение полей", result.Error);
    }

    /// <summary>Недоступный сервер не роняет страницу.</summary>
    [Fact]
    public async Task GetAsync_ServerUnreachable_ReturnsReadableError()
    {
        var authHandler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = await CreateSignedInStateAsync(authHandler);

        var handler = new FakeHttpHandler().RespondWithNetworkFailure();
        var api = CreateApiClient(handler, auth);

        var result = await api.GetAsync<GroupListResponseDto>("api/v1/admin/groups");

        Assert.Equal("Сервер недоступен", result.Error);
    }

    /// <summary>Испорченный ответ не превращается в необработанное исключение.</summary>
    [Fact]
    public async Task GetAsync_BrokenJson_ReturnsReadableError()
    {
        var authHandler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = await CreateSignedInStateAsync(authHandler);

        var handler = new FakeHttpHandler().Respond(HttpStatusCode.OK, "это не json");
        var api = CreateApiClient(handler, auth);

        var result = await api.GetAsync<GroupListResponseDto>("api/v1/admin/groups");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    /// <summary>
    /// Тело запроса уходит в JSON, а перечисления — по имени.
    /// </summary>
    /// <remarks>
    /// Сервер настроен принимать имена значений, а не их номера: номер зависит
    /// от порядка объявления, и его перестановка тихо поменяла бы роль
    /// заводимого пользователя.
    /// </remarks>
    [Fact]
    public async Task PostAsync_SerializesEnumsByName()
    {
        var authHandler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = await CreateSignedInStateAsync(authHandler);

        var handler = new FakeHttpHandler().Respond(HttpStatusCode.Created, """{"id":"u-2"}""");
        var api = CreateApiClient(handler, auth);

        await api.PostAsync("api/v1/admin/users", new CreateUserRequestDto
        {
            Username = "petrov",
            Password = "parol12345",
            Role = UserRole.Admin
        });

        var body = Assert.Single(handler.Requests).Body;
        Assert.Contains("\"role\":\"Admin\"", body);
        Assert.Contains("\"username\":\"petrov\"", body);
    }

    /// <summary>Удаление сообщает об успехе без разбора тела ответа.</summary>
    [Fact]
    public async Task DeleteAsync_NoContent_ReportsSuccess()
    {
        var authHandler = new FakeHttpHandler().Respond(HttpStatusCode.OK, LoginResponse(), "text/plain");
        var auth = await CreateSignedInStateAsync(authHandler);

        var handler = new FakeHttpHandler().Respond(HttpStatusCode.NoContent);
        var api = CreateApiClient(handler, auth);

        var result = await api.DeleteAsync("api/v1/admin/groups/g-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpMethod.Delete, Assert.Single(handler.Requests).Method);
    }
}
