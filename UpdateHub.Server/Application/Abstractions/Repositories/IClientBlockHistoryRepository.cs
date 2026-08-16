using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

/// <summary>Доступ к истории блокировок компьютеров.</summary>
public interface IClientBlockHistoryRepository : IRepository<ClientBlockHistoryEntity, string>
{
    /// <summary>Возвращает историю блокировок компьютера от новых записей к старым.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список записей.</returns>
    Task<IReadOnlyList<ClientBlockHistoryEntity>> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает причину последней блокировки компьютера.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Причина либо <see langword="null"/>.</returns>
    Task<string?> GetLatestBlockReasonAsync(string clientId, CancellationToken cancellationToken = default);
}
