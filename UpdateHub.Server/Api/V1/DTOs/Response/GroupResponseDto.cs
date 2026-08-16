namespace UpdateHub.Server.Api.V1.DTOs.Response;

/// <summary>Группа компьютеров в списке панели управления.</summary>
public class GroupResponseDto
{
    /// <summary>Идентификатор группы.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Название группы.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Описание группы.</summary>
    public string? Description { get; set; }

    /// <summary>Число активных компьютеров в группе.</summary>
    public int ClientCount { get; set; }

    /// <summary>Момент создания.</summary>
    public DateTime CreatedAt { get; set; }
}
