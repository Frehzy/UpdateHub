namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class UserGroupAccessDto
{
    public string GroupId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}