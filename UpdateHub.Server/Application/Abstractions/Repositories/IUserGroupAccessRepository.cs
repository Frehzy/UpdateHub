using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

/// <summary>Доступ к разрешениям на группы компьютеров.</summary>
public interface IUserGroupAccessRepository : IRepository<UserGroupAccessEntity, string>
{
    /// <summary>Проверяет наличие разрешения на группу.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если разрешение выдано.</returns>
    Task<bool> ExistsAsync(string userId, string groupId, CancellationToken cancellationToken = default);

    /// <summary>Ищет запись о разрешении.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Запись либо <see langword="null"/>.</returns>
    Task<UserGroupAccessEntity?> GetAsync(string userId, string groupId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает все разрешения пользователя на группы.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список разрешений.</returns>
    Task<IReadOnlyList<UserGroupAccessEntity>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
