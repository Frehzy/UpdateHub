namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class ClientResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string? GroupId { get; set; }
    public string? GroupName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? OsVersion { get; set; }
    public bool IsBlocked { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeen { get; set; }
}
