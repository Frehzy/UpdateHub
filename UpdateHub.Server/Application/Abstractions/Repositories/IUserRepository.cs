using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Domain.Enums;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

/// <summary>Доступ к учётным записям пользователей.</summary>
public interface IUserRepository : IRepository<UserEntity, string>
{
    /// <summary>Ищет пользователя по логину.</summary>
    /// <param name="username">Логин.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пользователь либо <see langword="null"/>.</returns>
    Task<UserEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>Возвращает активных пользователей указанной роли.</summary>
    /// <param name="role">Роль.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список пользователей.</returns>
    Task<IReadOnlyList<UserEntity>> GetByRoleAsync(UserRole role, CancellationToken cancellationToken = default);

    /// <summary>Проверяет, есть ли в системе хотя бы одна учётная запись.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если пользователей нет.</returns>
    Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default);

    /// <summary>Возвращает пользователя вместе с выданными ему правами.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пользователь либо <see langword="null"/>.</returns>
    Task<UserEntity?> GetByIdWithAccessAsync(string userId, CancellationToken cancellationToken = default);
}
