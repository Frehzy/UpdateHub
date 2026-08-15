namespace UpdateHub.Server.Api.V1.DTOs.Request;

public class AuthRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public ClientInfoDto? ClientInfo { get; set; }
}