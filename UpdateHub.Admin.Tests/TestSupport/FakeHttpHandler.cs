using System.Net;
using System.Text;

namespace UpdateHub.Admin.Tests.TestSupport;

/// <summary>
/// Подставной обработчик HTTP: отвечает тем, что задал тест.
/// </summary>
/// <remarks>
/// Настоящего сервера здесь нет намеренно. Проверяется поведение самой панели —
/// подставляет ли она заголовок авторизации, обновляет ли протухший токен,
/// как разбирает ошибки. Для этого нужны заранее заданные ответы, в том числе
/// такие, которых от исправного сервера не дождёшься: обрыв связи или
/// испорченный JSON.
/// </remarks>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

    /// <summary>Запросы, дошедшие до обработчика, в порядке отправки.</summary>
    public List<RecordedRequest> Requests { get; } = [];

    /// <summary>Добавляет очередной ответ.</summary>
    /// <param name="statusCode">Код состояния.</param>
    /// <param name="content">Тело ответа.</param>
    /// <param name="contentType">Тип содержимого.</param>
    /// <returns>Этот же обработчик — для цепочки вызовов.</returns>
    public FakeHttpHandler Respond(
        HttpStatusCode statusCode,
        string content = "",
        string contentType = "application/json")
    {
        _responses.Enqueue(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, Encoding.UTF8, contentType)
        });

        return this;
    }

    /// <summary>Добавляет ответ, изображающий недоступный сервер.</summary>
    /// <returns>Этот же обработчик — для цепочки вызовов.</returns>
    public FakeHttpHandler RespondWithNetworkFailure()
    {
        _responses.Enqueue(_ => throw new HttpRequestException("сеть недоступна"));
        return this;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri?.ToString() ?? string.Empty,
            request.Headers.Authorization?.Parameter,
            request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException(
                $"Тест не задал ответ на запрос {request.Method} {request.RequestUri}");
        }

        return _responses.Dequeue()(request);
    }

    /// <summary>Сведения об отправленном запросе.</summary>
    /// <param name="Method">Метод HTTP.</param>
    /// <param name="Url">Полный адрес.</param>
    /// <param name="BearerToken">Токен из заголовка авторизации или <c>null</c>.</param>
    /// <param name="Body">Тело запроса или <c>null</c>.</param>
    public sealed record RecordedRequest(HttpMethod Method, string Url, string? BearerToken, string? Body);
}
