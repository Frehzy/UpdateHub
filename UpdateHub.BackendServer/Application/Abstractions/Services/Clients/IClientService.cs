using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Domain.Entities.Clients;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.Shared.Contracts.Clients;

namespace UpdateHub.BackendServer.Application.Abstractions.Services.Clients;

/// <summary>Управление компьютерами и их характеристиками.</summary>
public interface IClientService
{
    /// <summary>Возвращает компьютер по идентификатору.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Компьютер либо <see langword="null"/>.</returns>
    Task<ClientEntity?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>Возвращает список компьютеров с фильтрацией.</summary>
    /// <param name="groupId">Ограничение по группе.</param>
    /// <param name="isBlocked">Ограничение по признаку блокировки.</param>
    /// <param name="search">Строка поиска.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список компьютеров.</returns>
    Task<IReadOnlyList<ClientEntity>> GetAllAsync(
        string? groupId,
        bool? isBlocked,
        string? search,
        CancellationToken cancellationToken = default);

    /// <summary>Возвращает подробные сведения о компьютере вместе с историей.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Подробные сведения.</returns>
    /// <exception cref="EntityNotFoundException">Компьютер не найден.</exception>
    Task<ClientDetailResponseDto> GetDetailAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>Заводит компьютер.</summary>
    /// <param name="request">Параметры создания.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданный компьютер.</returns>
    /// <exception cref="InvalidOperationException">Компьютер с таким идентификатором уже существует.</exception>
    Task<ClientEntity> CreateAsync(CreateClientRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Изменяет имя и группу компьютера.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="request">Новые значения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Изменённый компьютер.</returns>
    /// <exception cref="EntityNotFoundException">Компьютер или группа не найдены.</exception>
    Task<ClientEntity> UpdateAsync(string clientId, UpdateClientRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Помечает компьютер удалённым.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="EntityNotFoundException">Компьютер не найден.</exception>
    Task DeleteAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>Блокирует компьютер.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="reason">Причина, показываемая пользователю при отказе.</param>
    /// <param name="blockedBy">Логин администратора.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="EntityNotFoundException">Компьютер не найден.</exception>
    Task BlockAsync(string clientId, string reason, string blockedBy, CancellationToken cancellationToken = default);

    /// <summary>Снимает блокировку с компьютера.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="unblockedBy">Логин администратора.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="EntityNotFoundException">Компьютер не найден.</exception>
    Task UnblockAsync(string clientId, string unblockedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет сведения о компьютере по данным очередного обращения.
    /// Все расхождения попадают в историю изменений.
    /// </summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="report">Сведения, присланные клиентом.</param>
    /// <param name="context">Сведения о соединении.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task RecordCheckInAsync(
        string clientId,
        ClientReport report,
        ConnectionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Добавляет запись в историю изменений компьютера.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="changeType">Что изменилось.</param>
    /// <param name="oldValue">Прежнее значение.</param>
    /// <param name="newValue">Новое значение.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task AddHistoryAsync(
        string clientId,
        ClientChangeType changeType,
        string? oldValue,
        string? newValue,
        CancellationToken cancellationToken = default);
}
