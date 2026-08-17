using Microsoft.EntityFrameworkCore;
using System.Net;
using UpdateHub.Tests.TestSupport;

namespace UpdateHub.Tests.Api.V1.Controllers;

/// <summary>
/// Проверяет подачу заявки на регистрацию компьютера.
/// </summary>
/// <remarks>
/// Единственная точка, доступная совсем без учётных данных, и это осознанно:
/// новый компьютер ещё не заведён, и предъявить ему нечего. Заявка ничего
/// не открывает — она лишь сообщает администратору, что появилась машина,
/// которую стоит завести. До одобрения компьютер не получает ни файлов,
/// ни манифеста.
/// </remarks>
[Collection(ApiCollection.Name)]
public class EnrollControllerTests(UpdateHubApplication application)
{
    /// <summary>Отправляет заявку.</summary>
    /// <param name="client">Клиент.</param>
    /// <param name="fields">Поля формы.</param>
    /// <returns>Ответ сервера.</returns>
    private static Task<HttpResponseMessage> SubmitAsync(HttpClient client, params (string Key, string Value)[] fields)
        => client.PostAsync("/api/v1/enroll", new FormUrlEncodedContent(
            fields.Select(field => new KeyValuePair<string, string>(field.Key, field.Value))));

    /// <summary>Заявка принимается без токена и получает номер.</summary>
    [Fact]
    public async Task Submit_WithoutToken_Accepted()
    {
        using var client = application.CreateApiClient();
        var clientId = $"pc-zayavka-{Guid.NewGuid():N}";

        var response = await SubmitAsync(
            client,
            ("client_id", clientId),
            ("hostname", "sklad-03"),
            ("os_version", "Astra Linux 1.7.6"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var pairs = UpdateHubApplication.ParseTextPairs(await response.Content.ReadAsStringAsync());

        Assert.Equal("ok", pairs["status"]);
        Assert.False(string.IsNullOrEmpty(pairs["request_id"]));
        Assert.Equal("Pending", pairs["state"]);
    }

    /// <summary>Переданные сведения о машине сохраняются вместе с заявкой.</summary>
    /// <remarks>
    /// Администратор одобряет заявку не вслепую: он видит имя машины
    /// и версию системы и может сверить их с тем, что ожидает увидеть.
    /// </remarks>
    [Fact]
    public async Task Submit_StoresReportedDetails()
    {
        using var client = application.CreateApiClient();
        var clientId = $"pc-svedeniya-{Guid.NewGuid():N}";

        await SubmitAsync(
            client,
            ("client_id", clientId),
            ("hostname", "buhgalteriya-07"),
            ("os_version", "Astra Linux 1.7.6"),
            ("comment", "Замена вышедшего из строя компьютера"));

        await application.WithDatabaseAsync(async context =>
        {
            var request = await context.EnrollmentRequests.FirstOrDefaultAsync(x => x.ClientId == clientId);

            Assert.NotNull(request);
            Assert.Equal("buhgalteriya-07", request.Hostname);
            Assert.Equal("Astra Linux 1.7.6", request.OsVersion);
            Assert.Equal("Замена вышедшего из строя компьютера", request.Comment);
        });
    }

    /// <summary>Заявка без идентификатора компьютера отклоняется.</summary>
    [Fact]
    public async Task Submit_WithoutClientIdentifier_BadRequest()
    {
        using var client = application.CreateApiClient();

        var response = await SubmitAsync(client, ("hostname", "bez-identifikatora"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Заявка не заводит компьютер сама по себе: пока администратор её
    /// не одобрил, обмен для этой машины невозможен.
    /// </summary>
    [Fact]
    public async Task Submit_DoesNotCreateClient()
    {
        using var client = application.CreateApiClient();
        var clientId = $"pc-ne-zaveden-{Guid.NewGuid():N}";

        await SubmitAsync(client, ("client_id", clientId));

        await application.WithDatabaseAsync(async context =>
        {
            var exists = await context.Clients.AnyAsync(x => x.Id == clientId);
            Assert.False(exists, "Заявка не должна заводить компьютер до одобрения");
        });
    }

    /// <summary>Ответ приходит текстом, а не JSON.</summary>
    [Fact]
    public async Task Submit_AnswersWithPlainText()
    {
        using var client = application.CreateApiClient();

        var response = await SubmitAsync(client, ("client_id", $"pc-tekst-{Guid.NewGuid():N}"));

        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
    }
}
