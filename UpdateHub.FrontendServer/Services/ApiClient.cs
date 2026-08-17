using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json.Serialization;
using System.Text.Json;
using UpdateHub.Shared.Contracts.Common;

namespace UpdateHub.FrontendServer.Services;

/// <summary>
/// Результат обращения к API.
/// </summary>
/// <typeparam name="T">Тип полезной нагрузки.</typeparam>
/// <param name="Value">Ответ сервера или <c>null</c> при ошибке.</param>
/// <param name="Error">Сообщение об ошибке или <c>null</c> при успехе.</param>
/// <remarks>
/// Исключения наружу не выбрасываются намеренно. Каждая страница обязана
/// показать причину отказа человеку, а не белый экран, и работать с
/// результатом проще, чем оборачивать каждый вызов в try.
/// </remarks>
public sealed record ApiResult<T>(T? Value, string? Error)
{
    /// <summary>Успешно ли выполнено обращение.</summary>
    public bool IsSuccess => Error is null;
}

/// <summary>
/// Обращения к панельной части API.
/// </summary>
/// <remarks>
/// Берёт на себя две вещи, которые иначе пришлось бы повторять на каждой
/// странице: подстановку заголовка авторизации и обновление протухшего
/// access-токена. Срок жизни access-токена короткий, и без обновления
/// администратор получал бы «требуется вход» посреди работы.
/// </remarks>
/// <param name="http">Клиент HTTP.</param>
/// <param name="auth">Состояние входа.</param>
public sealed class ApiClient(HttpClient http, AuthState auth)
{
    /// <summary>
    /// Настройки разбора JSON, совпадающие с настройками сервера.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Запрашивает данные.</summary>
    /// <typeparam name="T">Тип ответа.</typeparam>
    /// <param name="url">Адрес.</param>
    /// <returns>Результат обращения.</returns>
    public Task<ApiResult<T>> GetAsync<T>(string url)
        => SendAsync<T>(HttpMethod.Get, url, body: null);

    /// <summary>Отправляет данные и ожидает ответ.</summary>
    /// <typeparam name="T">Тип ответа.</typeparam>
    /// <param name="url">Адрес.</param>
    /// <param name="body">Тело запроса.</param>
    /// <returns>Результат обращения.</returns>
    public Task<ApiResult<T>> PostAsync<T>(string url, object? body)
        => SendAsync<T>(HttpMethod.Post, url, body);

    /// <summary>Отправляет данные без разбора ответа.</summary>
    /// <param name="url">Адрес.</param>
    /// <param name="body">Тело запроса.</param>
    /// <returns>Результат обращения.</returns>
    public Task<ApiResult<bool>> PostAsync(string url, object? body = null)
        => SendWithoutContentAsync(HttpMethod.Post, url, body);

    /// <summary>Изменяет существующую запись.</summary>
    /// <param name="url">Адрес.</param>
    /// <param name="body">Тело запроса.</param>
    /// <returns>Результат обращения.</returns>
    public Task<ApiResult<bool>> PutAsync(string url, object? body = null)
        => SendWithoutContentAsync(HttpMethod.Put, url, body);

    /// <summary>Удаляет запись.</summary>
    /// <param name="url">Адрес.</param>
    /// <returns>Результат обращения.</returns>
    public Task<ApiResult<bool>> DeleteAsync(string url)
        => SendWithoutContentAsync(HttpMethod.Delete, url, body: null);

    /// <summary>Выполняет запрос и разбирает ответ.</summary>
    /// <typeparam name="T">Тип ответа.</typeparam>
    /// <param name="method">Метод HTTP.</param>
    /// <param name="url">Адрес.</param>
    /// <param name="body">Тело запроса.</param>
    /// <returns>Результат обращения.</returns>
    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body)
    {
        var response = await SendWithRetryAsync(method, url, body);
        if (response is null)
        {
            return new ApiResult<T>(default, "Сервер недоступен");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return new ApiResult<T>(default, await DescribeFailureAsync(response));
            }

            try
            {
                var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
                return new ApiResult<T>(value, null);
            }
            catch (JsonException)
            {
                return new ApiResult<T>(default, "Сервер вернул ответ, который не удалось разобрать");
            }
        }
    }

    /// <summary>Выполняет запрос, ответ которого не нужен.</summary>
    /// <param name="method">Метод HTTP.</param>
    /// <param name="url">Адрес.</param>
    /// <param name="body">Тело запроса.</param>
    /// <returns>Результат обращения.</returns>
    private async Task<ApiResult<bool>> SendWithoutContentAsync(HttpMethod method, string url, object? body)
    {
        var response = await SendWithRetryAsync(method, url, body);
        if (response is null)
        {
            return new ApiResult<bool>(false, "Сервер недоступен");
        }

        using (response)
        {
            return response.IsSuccessStatusCode
                ? new ApiResult<bool>(true, null)
                : new ApiResult<bool>(false, await DescribeFailureAsync(response));
        }
    }

    /// <summary>
    /// Выполняет запрос, обновив токен и повторив попытку при ответе 401.
    /// </summary>
    /// <param name="method">Метод HTTP.</param>
    /// <param name="url">Адрес.</param>
    /// <param name="body">Тело запроса.</param>
    /// <returns>Ответ сервера или <c>null</c>, если сервер недоступен.</returns>
    /// <remarks>
    /// Повтор ровно один. Если и после обновления токена сервер отвечает 401,
    /// значит дело не в сроке жизни токена, и второй заход ничего не изменит.
    /// </remarks>
    private async Task<HttpResponseMessage?> SendWithRetryAsync(HttpMethod method, string url, object? body)
    {
        var response = await TrySendAsync(method, url, body);

        if (response?.StatusCode == HttpStatusCode.Unauthorized && await auth.TryRefreshAsync())
        {
            response.Dispose();
            response = await TrySendAsync(method, url, body);
        }

        return response;
    }

    /// <summary>Отправляет один запрос.</summary>
    /// <param name="method">Метод HTTP.</param>
    /// <param name="url">Адрес.</param>
    /// <param name="body">Тело запроса.</param>
    /// <returns>Ответ сервера или <c>null</c>, если он недоступен.</returns>
    private async Task<HttpResponseMessage?> TrySendAsync(HttpMethod method, string url, object? body)
    {
        var request = new HttpRequestMessage(method, url);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: JsonOptions);
        }

        if (!string.IsNullOrEmpty(auth.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        }

        try
        {
            return await http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    /// <summary>
    /// Превращает неуспешный ответ в сообщение для человека.
    /// </summary>
    /// <param name="response">Ответ сервера.</param>
    /// <returns>Сообщение об ошибке.</returns>
    /// <remarks>
    /// Панельная часть отвечает JSON с полем <c>error</c>, но при отказе
    /// проверки формы ASP.NET Core присылает своё описание, а слой авторизации —
    /// вообще текст. Поэтому разбор идёт по возможности, а при неудаче
    /// показывается код состояния: он всё равно полезнее пустоты.
    /// </remarks>
    private static async Task<string> DescribeFailureAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();

        if (text.StartsWith('{'))
        {
            try
            {
                var error = JsonSerializer.Deserialize<ErrorResponseDto>(text, JsonOptions);
                if (!string.IsNullOrWhiteSpace(error?.Error))
                {
                    return error.Error;
                }
            }
            catch (JsonException)
            {
            }
        }

        if (text.StartsWith("error=", StringComparison.Ordinal))
        {
            return TextProtocol.ExtractError(text, "Ошибка обращения к серверу");
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Требуется повторный вход",
            HttpStatusCode.Forbidden => "Недостаточно прав для этой операции",
            HttpStatusCode.NotFound => "Запись не найдена",
            HttpStatusCode.Conflict => "Такая запись уже существует",
            HttpStatusCode.BadRequest => "Проверьте заполнение полей",
            _ => $"Ошибка обращения к серверу ({(int)response.StatusCode})"
        };
    }
}
