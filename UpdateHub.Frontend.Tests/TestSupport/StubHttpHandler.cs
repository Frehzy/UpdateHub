using System.Net;
using System.Text;
using System.Text.Json;

namespace UpdateHub.Frontend.Tests.TestSupport;

/// <summary>
/// Подставной обработчик запросов: отвечает заранее заданными данными.
/// </summary>
/// <remarks>
/// Нужен для проверок разметки: компонент обращается к серверу через настоящий
/// <see cref="HttpClient"/>, и подменять приходится самый низ — иначе пришлось бы
/// делать <c>ApiClient</c> заменяемым, то есть менять рабочий код ради проверок.
/// <para>
/// Ответы собираются из настоящих типов контрактов, а не из написанного руками
/// JSON: тогда несовпадение имён полей невозможно в принципе. Настройки разбора
/// те же, что у <c>ApiClient</c> и у сервера — <see cref="JsonSerializerDefaults.Web"/>.
/// </para>
/// </remarks>
public sealed class StubHttpHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, string> _byPath = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Задаёт ответ на обращение к адресу.
    /// </summary>
    /// <typeparam name="T">Тип ответа.</typeparam>
    /// <param name="path">Адрес без ведущей косой черты, как его пишет компонент.</param>
    /// <param name="payload">Объект, который будет отдан в виде JSON.</param>
    /// <returns>Этот же обработчик — для цепочки вызовов.</returns>
    public StubHttpHandler Respond<T>(string path, T payload)
    {
        _byPath[path.TrimStart('/')] = JsonSerializer.Serialize(payload, JsonOptions);
        return this;
    }

    /// <inheritdoc />
    /// <remarks>
    /// На незаданный адрес отвечает 404: так забытый в проверке запрос виден
    /// сразу, а не превращается в пустое значение неизвестного происхождения.
    /// </remarks>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath.TrimStart('/') ?? string.Empty;

        if (!_byPath.TryGetValue(path, out var json))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent($"error=Ответ для {path} не задан\n", Encoding.UTF8, "text/plain")
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
    }
}
