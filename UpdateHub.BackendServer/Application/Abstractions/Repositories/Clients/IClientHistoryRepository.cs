using UpdateHub.BackendServer.Domain.Entities.Clients;

namespace UpdateHub.BackendServer.Application.Abstractions.Repositories.Clients;

/// <summary>Доступ к истории изменений характеристик компьютеров.</summary>
public interface IClientHistoryRepository : IRepository<ClientHistoryEntity, int>
{
    /// <summary>Возвращает последние записи истории компьютера.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="limit">Максимальное число записей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список записей от новых к старым.</returns>
    Task<IReadOnlyList<ClientHistoryEntity>> GetByClientIdAsync(string clientId, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>Удаляет записи старше указанного момента.</summary>
    /// <param name="cutoff">Граничный момент времени.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Число удалённых записей.</returns>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
