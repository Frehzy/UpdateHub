namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class GroupResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ClientCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}