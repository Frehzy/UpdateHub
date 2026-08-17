using UpdateHub.BackendServer.Domain.Entities.Updates;

namespace UpdateHub.BackendServer.Application.Abstractions.Repositories.Updates;

/// <summary>Доступ к журналу обращений клиентов.</summary>
public interface IUpdateRequestRepository : IRepository<UpdateRequestEntity, int>
{
    /// <summary>Возвращает последние обращения компьютера.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="limit">Максимальное число записей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список обращений от новых к старым.</returns>
    Task<IReadOnlyList<UpdateRequestEntity>> GetByClientIdAsync(string clientId, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Считает сводную статистику запросом к базе, не выгружая таблицу в память.
    /// </summary>
    /// <param name="from">Нижняя граница периода; <see langword="null"/> — без ограничения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сводка по обращениям.</returns>
    Task<RequestSummary> GetSummaryAsync(DateTime? from, CancellationToken cancellationToken = default);

    /// <summary>Считает число обращений по дням.</summary>
    /// <param name="from">Нижняя граница периода; <see langword="null"/> — без ограничения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пары «дата — число обращений», отсортированные по дате.</returns>
    Task<IReadOnlyList<(DateTime Date, int Count)>> GetDailyCountsAsync(DateTime? from, CancellationToken cancellationToken = default);

    /// <summary>Удаляет обращения старше указанного момента.</summary>
    /// <param name="cutoff">Граничный момент времени.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Число удалённых записей.</returns>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}

/// <summary>
/// Сводная статистика обращений.
/// </summary>
/// <param name="TotalRequests">Общее число обращений.</param>
/// <param name="UniqueClients">Число различных компьютеров.</param>
/// <param name="TotalBytes">Суммарный объём файлов, предложенных к скачиванию.</param>
public sealed record RequestSummary(int TotalRequests, int UniqueClients, long TotalBytes);
