using UpdateHub.BackendServer.Domain.Entities;

namespace UpdateHub.BackendServer.Application.Abstractions.Repositories;

/// <summary>Доступ к персональным разрешениям на компьютеры.</summary>
public interface IUserClientAccessRepository : IRepository<UserClientAccessEntity, string>
{
    /// <summary>Проверяет наличие персонального разрешения.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если разрешение выдано.</returns>
    Task<bool> ExistsAsync(string userId, string clientId, CancellationToken cancellationToken = default);

    /// <summary>Ищет запись о разрешении.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Запись либо <see langword="null"/>.</returns>
    Task<UserClientAccessEntity?> GetAsync(string userId, string clientId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает все персональные разрешения пользователя.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список разрешений.</returns>
    Task<IReadOnlyList<UserClientAccessEntity>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
