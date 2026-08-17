using UpdateHub.BackendServer.Domain.Entities.Groups;
using UpdateHub.BackendServer.Domain.Entities.Updates;
using UpdateHub.BackendServer.Domain.Entities.Users;

namespace UpdateHub.BackendServer.Domain.Entities.Clients;

/// <summary>
/// Компьютер, обслуживаемый сервером обновлений.
/// </summary>
/// <remarks>
/// Записи создаются только администратором либо одобрением заявки
/// (<see cref="EnrollmentRequestEntity"/>). Обращение с неизвестным
/// идентификатором отклоняется, а не заводит нового клиента автоматически.
/// </remarks>
public class ClientEntity
{
    /// <summary>
    /// Идентификатор компьютера — UUID, который скрипт хранит
    /// в <c>/etc/updatehub/client-id</c> и присылает при каждом обращении.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Группа компьютеров, к которой относится машина. Может отсутствовать.</summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Признак блокировки. Заблокированному компьютеру отказано в синхронизации
    /// и скачивании файлов, даже если у пользователя есть права на него.
    /// </summary>
    public bool IsBlocked { get; set; }

    /// <summary>
    /// Признак активности. Снимается при «мягком удалении» компьютера
    /// и равносилен отсутствию записи для всех клиентских операций.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Момент создания записи.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Момент последнего изменения записи.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Группа компьютеров (навигационное свойство).</summary>
    public GroupEntity? Group { get; set; }

    /// <summary>Сведения о железе и операционной системе.</summary>
    public ClientComputerInfoEntity? ComputerInfo { get; set; }

    /// <summary>Известные сетевые адреса компьютера.</summary>
    public ICollection<ClientNetworkInfoEntity> NetworkInfos { get; set; } = [];

    /// <summary>История блокировок и разблокировок.</summary>
    public ICollection<ClientBlockHistoryEntity> BlockHistory { get; set; } = [];

    /// <summary>История изменений характеристик компьютера.</summary>
    public ICollection<ClientHistoryEntity> History { get; set; } = [];

    /// <summary>Персональные разрешения пользователей на этот компьютер.</summary>
    public ICollection<UserClientAccessEntity> UserClientAccesses { get; set; } = [];

    /// <summary>Журнал обращений к серверу обновлений.</summary>
    public ICollection<UpdateRequestEntity> UpdateRequests { get; set; } = [];
}
