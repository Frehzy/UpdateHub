namespace UpdateHub.BackendServer.Application.Abstractions.Repositories.Updates;

/// <summary>
/// Сводная статистика обращений.
/// </summary>
/// <param name="TotalRequests">Общее число обращений.</param>
/// <param name="UniqueClients">Число различных компьютеров.</param>
/// <param name="TotalBytes">Суммарный объём файлов, предложенных к скачиванию.</param>
/// <remarks>
/// Возвращается методом <see cref="IUpdateRequestRepository.GetSummaryAsync"/>.
/// Тип живёт рядом с описанием хранилища, а не в общих контрактах: наружу,
/// в панель управления, уходит <c>StatisticsDto</c>, а это промежуточный
/// результат агрегирующего запроса, и знать о нём должен только сервер.
/// </remarks>
public sealed record RequestSummary(int TotalRequests, int UniqueClients, long TotalBytes);
