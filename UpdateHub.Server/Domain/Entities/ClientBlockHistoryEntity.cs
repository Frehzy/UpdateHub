namespace UpdateHub.Server.Domain.Entities;

/// <summary>
/// Запись о блокировке или разблокировке компьютера администратором.
/// </summary>
public class ClientBlockHistoryEntity
{
    /// <summary>Первичный ключ.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Компьютер, к которому относится запись.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Выполненное действие: <c>blocked</c> или <c>unblocked</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Причина блокировки, показываемая клиенту при отказе.</summary>
    public string? Reason { get; set; }

    /// <summary>Логин администратора, выполнившего действие.</summary>
    public string? BlockedBy { get; set; }

    /// <summary>Момент выполнения действия.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Компьютер (навигационное свойство).</summary>
    public ClientEntity? Client { get; set; }
}
