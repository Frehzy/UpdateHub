namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class UserResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLogin { get; set; }
    public List<UserClientAccessDto>? ClientAccesses { get; set; }
    public List<UserGroupAccessDto>? GroupAccesses { get; set; }
}