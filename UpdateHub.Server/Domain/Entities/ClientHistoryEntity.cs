using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Domain.Entities;

/// <summary>
/// Запись об изменении характеристики компьютера — имени, железа, адреса, группы.
/// </summary>
public class ClientHistoryEntity
{
    /// <summary>Первичный ключ (автоинкремент).</summary>
    public int Id { get; set; }

    /// <summary>Компьютер, к которому относится изменение.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Что именно изменилось.</summary>
    public ClientChangeType ChangeType { get; set; }

    /// <summary>Прежнее значение.</summary>
    public string? OldValue { get; set; }

    /// <summary>Новое значение.</summary>
    public string? NewValue { get; set; }

    /// <summary>Момент фиксации изменения.</summary>
    public DateTime ChangeTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Компьютер (навигационное свойство).</summary>
    public ClientEntity? Client { get; set; }
}
