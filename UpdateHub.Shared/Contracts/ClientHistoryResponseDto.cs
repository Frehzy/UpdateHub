namespace UpdateHub.Shared.Contracts;

/// <summary>Запись истории изменений компьютера.</summary>
public class ClientHistoryResponseDto
{
    /// <summary>Что именно изменилось.</summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>Прежнее значение.</summary>
    public string? OldValue { get; set; }

    /// <summary>Новое значение.</summary>
    public string? NewValue { get; set; }

    /// <summary>Момент изменения.</summary>
    public DateTime ChangeTimestamp { get; set; }
}
