namespace UpdateHub.Shared.Contracts.Clients;

/// <summary>Компьютер в списке панели управления.</summary>
public class ClientResponseDto
{
    /// <summary>Идентификатор компьютера.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Идентификатор группы.</summary>
    public string? GroupId { get; set; }

    /// <summary>Название группы.</summary>
    public string? GroupName { get; set; }

    /// <summary>Отображаемое имя компьютера.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Последний известный сетевой адрес.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Версия операционной системы.</summary>
    public string? OsVersion { get; set; }

    /// <summary>Признак блокировки.</summary>
    public bool IsBlocked { get; set; }

    /// <summary>Признак активности.</summary>
    public bool IsActive { get; set; }

    /// <summary>Момент регистрации.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Момент последнего обращения.</summary>
    public DateTime? LastSeen { get; set; }
}
