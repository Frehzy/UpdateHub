using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Services;

/// <summary>Управление группами компьютеров и выдачей прав.</summary>
public interface IGroupService
{
    /// <summary>Создаёт группу.</summary>
    /// <param name="name">Название.</param>
    /// <param name="description">Описание.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданная группа.</returns>
    /// <exception cref="InvalidOperationException">Группа с таким названием уже есть.</exception>
    Task<GroupEntity> CreateAsync(string name, string? description, CancellationToken cancellationToken = default);

    /// <summary>Изменяет название и описание группы.</summary>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="name">Новое название либо <see langword="null"/>.</param>
    /// <param name="description">Новое описание либо <see langword="null"/>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Изменённая группа.</returns>
    /// <exception cref="EntityNotFoundException">Группа не найдена.</exception>
    Task<GroupEntity> UpdateAsync(string groupId, string? name, string? description, CancellationToken cancellationToken = default);

    /// <summary>Помечает группу удалённой.</summary>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="EntityNotFoundException">Группа не найдена.</exception>
    Task DeleteAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает активные группы.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список групп.</returns>
    Task<IReadOnlyList<GroupResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Возвращает группу вместе со списком её компьютеров.</summary>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Подробные сведения о группе.</returns>
    /// <exception cref="EntityNotFoundException">Группа не найдена.</exception>
    Task<GroupDetailResponseDto> GetDetailAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>Выдаёт пользователю права на конкретный компьютер.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="EntityNotFoundException">Пользователь или компьютер не найдены.</exception>
    Task GrantClientAccessAsync(string userId, string clientId, CancellationToken cancellationToken = default);

    /// <summary>Отзывает права пользователя на компьютер.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="EntityNotFoundException">Разрешение не найдено.</exception>
    Task RevokeClientAccessAsync(string userId, string clientId, CancellationToken cancellationToken = default);

    /// <summary>Выдаёт пользователю права на группу компьютеров.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="EntityNotFoundException">Пользователь или группа не найдены.</exception>
    Task GrantGroupAccessAsync(string userId, string groupId, CancellationToken cancellationToken = default);

    /// <summary>Отзывает права пользователя на группу.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="groupId">Идентификатор группы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="EntityNotFoundException">Разрешение не найдено.</exception>
    Task RevokeGroupAccessAsync(string userId, string groupId, CancellationToken cancellationToken = default);
}
