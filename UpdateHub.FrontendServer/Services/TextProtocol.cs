namespace UpdateHub.FrontendServer.Services;

/// <summary>
/// Разбор ответов клиентской части API.
/// </summary>
/// <remarks>
/// Вход и обновление токенов отвечают строками «ключ=значение», а не JSON:
/// эти же адреса вызывает bash-скрипт на компьютере, у которого нет jq.
/// Панель пользуется теми же адресами и разбирает тот же формат — заводить
/// ради браузера второй способ входа значило бы дублировать проверку пароля.
/// </remarks>
public static class TextProtocol
{
    /// <summary>Разбирает ответ вида «ключ=значение».</summary>
    /// <param name="text">Тело ответа.</param>
    /// <returns>Пары «ключ — значение».</returns>
    public static Dictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                result[line[..separator]] = line[(separator + 1)..];
            }
        }

        return result;
    }

    /// <summary>
    /// Достаёт сообщение об ошибке из текстового ответа.
    /// </summary>
    /// <param name="text">Тело ответа.</param>
    /// <param name="fallback">Что вернуть, если сообщения нет.</param>
    /// <returns>Сообщение для показа человеку.</returns>
    public static string ExtractError(string text, string fallback)
        => Parse(text).TryGetValue("error", out var message) && message.Length > 0 ? message : fallback;
}
