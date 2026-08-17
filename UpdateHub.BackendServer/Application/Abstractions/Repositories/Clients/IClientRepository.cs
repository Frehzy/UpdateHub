using UpdateHub.BackendServer.Domain.Entities.Clients;

namespace UpdateHub.BackendServer.Application.Abstractions.Repositories.Clients;

/// <summary>Доступ к компьютерам.</summary>
public interface IClientRepository : IRepository<ClientEntity, string>
{
    /// <summary>Возвращает компьютер вместе со связанными сведениями и историей.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Компьютер либо <see langword="null"/>.</returns>
    Task<ClientEntity?> GetByIdWithDetailsAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает активный компьютер вместе со сведениями о железе.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Компьютер либо <see langword="null"/>.</returns>
    Task<ClientEntity?> GetActiveWithInfoAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает активные компьютеры с фильтрацией.</summary>
    /// <param name="groupId">Ограничение по группе.</param>
    /// <param name="isBlocked">Ограничение по признаку блокировки.</param>
    /// <param name="search">Подстрока для поиска по идентификатору и имени.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список компьютеров.</returns>
    Task<IReadOnlyList<ClientEntity>> SearchAsync(
        string? groupId = null,
        bool? isBlocked = null,
        string? search = null,
        CancellationToken cancellationToken = default);
}
