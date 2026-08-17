using System.Net;
using UpdateHub.Tests.TestSupport;

namespace UpdateHub.Tests.Api.V1.Controllers;

/// <summary>
/// Проверяет справочную страницу и проверку работоспособности.
/// </summary>
/// <remarks>
/// Обе точки доступны без токена — это единственные адреса, кроме входа
/// и заявки, которые можно открыть браузером. Появились они не для красоты:
/// администратор, открывший корень сервера и получивший 404, первым делом
/// решает, что сервер сломан.
/// </remarks>
[Collection(ApiCollection.Name)]
public class IndexControllerTests(UpdateHubApplication application)
{
    /// <summary>Справка отвечает по обоим адресам без токена.</summary>
    /// <param name="path">Проверяемый адрес.</param>
    /// <remarks>
    /// Корневой адрес в список не входит: его занимает панель управления.
    /// Проверить её отсюда нельзя — файлы панели появляются только после
    /// сборки браузерного приложения, а тесты поднимают один сервер.
    /// </remarks>
    [Theory]
    [InlineData("/api")]
    [InlineData("/api/v1")]
    public async Task Index_AvailableWithoutToken(string path)
    {
        using var client = application.CreateApiClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Справка перечисляет адреса клиентской части: именно по ней настраивают
    /// скрипт на компьютере, у которого нет ни Swagger, ни документации.
    /// </summary>
    [Fact]
    public async Task Index_ListsClientEndpoints()
    {
        using var client = application.CreateApiClient();

        var text = await client.GetStringAsync("/api");

        Assert.Contains("/api/v1/auth/login", text, StringComparison.Ordinal);
        Assert.Contains("/api/v1/sync/diff", text, StringComparison.Ordinal);
        Assert.Contains("/api/v1/files", text, StringComparison.Ordinal);
        Assert.Contains("/api/v1/enroll", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Проверка работоспособности доступна без токена: её опрашивает Docker,
    /// у которого токена нет и быть не может.
    /// </summary>
    [Fact]
    public async Task Health_AvailableWithoutToken()
    {
        using var client = application.CreateApiClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Несуществующий адрес под <c>/api</c> отвечает 404 текстом.
    /// </summary>
    /// <remarks>
    /// Проверка не формальная. Панель управления — одностраничное приложение,
    /// и ради неё сервер отдаёт свою страницу на любой неизвестный адрес.
    /// Если бы это правило распространилось на <c>/api</c>, bash-скрипт получил
    /// бы на опечатку в адресе код 200 и HTML вместо ошибки — и продолжил бы
    /// работать, считая, что всё в порядке.
    /// </remarks>
    [Fact]
    public async Task UnknownApiPath_ReturnsPlainTextNotFound()
    {
        using var client = application.CreateApiClient();

        var response = await client.GetAsync("/api/v1/takogo-adresa-net");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("error=", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
