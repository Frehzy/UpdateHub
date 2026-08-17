namespace UpdateHub.Shared.Contracts.Groups;

/// <summary>Список групп.</summary>
public class GroupListResponseDto
{
    /// <summary>Группы.</summary>
    public List<GroupResponseDto> Groups { get; set; } = [];

    /// <summary>Общее количество.</summary>
    public int Total { get; set; }
}
