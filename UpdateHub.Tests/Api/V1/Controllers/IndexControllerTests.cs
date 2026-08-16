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
    /// <summary>Справка отвечает по всем трём адресам без токена.</summary>
    /// <param name="path">Проверяемый адрес.</param>
    [Theory]
    [InlineData("/")]
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

        var text = await client.GetStringAsync("/");

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

    /// <summary>Несуществующий адрес отвечает 404, а не падением.</summary>
    [Fact]
    public async Task UnknownPath_ReturnsNotFound()
    {
        using var client = application.CreateApiClient();

        var response = await client.GetAsync("/api/v1/takogo-adresa-net");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
