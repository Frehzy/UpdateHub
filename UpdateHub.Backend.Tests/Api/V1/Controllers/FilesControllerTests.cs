using System.Net.Http.Headers;
using System.Net;
using UpdateHub.Backend.Tests.TestSupport;

namespace UpdateHub.Backend.Tests.Api.V1.Controllers;

/// <summary>
/// Проверяет скачивание файлов.
/// </summary>
/// <remarks>
/// Самая нагруженная часть сервера: через неё уходит образ размером около
/// шести гигабайт по каналу, который иногда опускается до 2 Мбит/с. Такая
/// передача идёт часами и рвётся, поэтому важны не только права доступа,
/// но и возможность докачать файл с прерванного места — <c>curl -C -</c>
/// опирается на поддержку диапазонов и на неизменный ETag.
/// </remarks>
[Collection(ApiCollection.Name)]
public class FilesControllerTests(UpdateHubApplication application)
{
    /// <summary>Контрольная сумма строки «hello», посчитанная <c>md5sum</c>.</summary>
    private const string HelloMd5 = "5d41402abc4b2a76b9719d911017c592";

    /// <summary>Готовит пользователя с правами на свой компьютер.</summary>
    /// <returns>Клиент с токеном и идентификатор компьютера.</returns>
    /// <remarks>
    /// Логин и идентификатор компьютера делаются уникальными: приложение общее
    /// на всю группу тестов, а логин в базе обязан быть единственным.
    /// </remarks>
    private async Task<(HttpClient Client, string ClientId)> CreateClientWithAccessAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"files-{suffix}";
        var clientId = $"pc-files-{suffix}";

        var user = await application.AddUserAsync(username, "parol12345");
        await application.AddClientAsync(clientId, user.Id);

        return (await application.CreateAuthorizedClientAsync(username, "parol12345"), clientId);
    }

    /// <summary>Без токена файл не отдаётся.</summary>
    [Fact]
    public async Task Download_WithoutToken_Unauthorized()
    {
        using var client = application.CreateApiClient();

        var response = await client.GetAsync("/api/v1/files?client_id=pc-1&path=docs/privet.txt");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>Файл отдаётся целиком и совпадает с исходным.</summary>
    [Fact]
    public async Task Download_ReturnsFileContent()
    {
        await application.PublishFileAsync("docs/skachat.txt", "hello");
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            var response = await client.GetAsync($"/api/v1/files?client_id={clientId}&path=docs/skachat.txt");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("hello", await response.Content.ReadAsStringAsync());
        }
    }

    /// <summary>
    /// В ETag отдаётся контрольная сумма файла.
    /// </summary>
    /// <remarks>
    /// На ней держится докачка: <c>curl -C -</c> продолжает загрузку только
    /// если сервер подтвердил, что файл с прошлого раза не изменился.
    /// </remarks>
    [Fact]
    public async Task Download_ReturnsChecksumAsEntityTag()
    {
        await application.PublishFileAsync("docs/etag.txt", "hello");
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            var response = await client.GetAsync($"/api/v1/files?client_id={clientId}&path=docs/etag.txt");

            Assert.Equal($"\"{HelloMd5}\"", response.Headers.ETag?.Tag);
        }
    }

    /// <summary>
    /// Запрос диапазона возвращает кусок файла и код 206.
    /// </summary>
    /// <remarks>
    /// Без этого прерванная на пятом гигабайте закачка начиналась бы заново,
    /// а на канале 2 Мбит/с это ещё семь часов.
    /// </remarks>
    [Fact]
    public async Task Download_RangeRequest_ReturnsPartialContent()
    {
        await application.PublishFileAsync("docs/dokachka.txt", "hello");
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/v1/files?client_id={clientId}&path=docs/dokachka.txt");
            request.Headers.Range = new RangeHeaderValue(2, 4);

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
            Assert.Equal("llo", await response.Content.ReadAsStringAsync());
        }
    }

    /// <summary>Отсутствующий файл — 404 с сообщением текстом.</summary>
    [Fact]
    public async Task Download_MissingFile_NotFound()
    {
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            var response = await client.GetAsync($"/api/v1/files?client_id={clientId}&path=docs/takogo-net.txt");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.StartsWith("error=", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Выход за пределы каталога раздачи не работает ни в каком виде.
    /// </summary>
    /// <remarks>
    /// Путь приходит от клиента и подставляется в обращение к файловой системе.
    /// Проверяются оба написания: и переход вверх, и абсолютный путь.
    /// </remarks>
    /// <param name="path">Путь, которым пытаются выйти из каталога.</param>
    [Theory]
    [InlineData("../appsettings.json")]
    [InlineData("../../etc/passwd")]
    [InlineData("/etc/passwd")]
    [InlineData("docs/../../etc/passwd")]
    public async Task Download_PathOutsideFilesDirectory_Rejected(string path)
    {
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            var response = await client.GetAsync(
                $"/api/v1/files?client_id={clientId}&path={Uri.EscapeDataString(path)}");

            Assert.True(
                response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
                $"Путь '{path}' получил ответ {(int)response.StatusCode}");
        }
    }

    /// <summary>Файл не отдаётся, если компьютер не выдан пользователю.</summary>
    [Fact]
    public async Task Download_ClientWithoutGrant_Forbidden()
    {
        await application.PublishFileAsync("docs/chuzhoy.txt", "hello");
        var (client, _) = await CreateClientWithAccessAsync();
        await application.AddClientAsync("pc-files-nichey");

        using (client)
        {
            var response = await client.GetAsync("/api/v1/files?client_id=pc-files-nichey&path=docs/chuzhoy.txt");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    /// <summary>Запрос без обязательных параметров отклоняется.</summary>
    [Fact]
    public async Task Download_WithoutParameters_BadRequest()
    {
        var (client, _) = await CreateClientWithAccessAsync();

        using (client)
        {
            var response = await client.GetAsync("/api/v1/files");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
