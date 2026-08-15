namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class UserClientAccessDto
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}