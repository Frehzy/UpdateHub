namespace UpdateHub.BackendServer.Application.Sync;

/// <summary>
/// Сведения о соединении, взятые из HTTP-контекста.
/// </summary>
/// <param name="RemoteIpAddress">
/// Адрес клиента из самого соединения. Берётся именно отсюда, а не из тела
/// запроса: адрес, названный клиентом, ничем не подтверждён.
/// </param>
/// <param name="UserAgent">Значение заголовка User-Agent.</param>
public sealed record ConnectionContext(string? RemoteIpAddress, string? UserAgent);
