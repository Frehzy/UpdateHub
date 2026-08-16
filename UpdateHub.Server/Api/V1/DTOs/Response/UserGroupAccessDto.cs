namespace UpdateHub.Server.Api.V1.DTOs.Response;

/// <summary>Разрешение пользователя на группу компьютеров.</summary>
public class UserGroupAccessDto
{
    /// <summary>Идентификатор группы.</summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>Название группы.</summary>
    public string? GroupName { get; set; }

    /// <summary>Момент выдачи разрешения.</summary>
    public DateTime CreatedAt { get; set; }
}
