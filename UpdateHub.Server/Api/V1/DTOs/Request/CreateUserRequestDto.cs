namespace UpdateHub.Server.Api.V1.DTOs.Request;

public class CreateUserRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Role { get; set; } = "Client";
    public List<string>? GroupIds { get; set; }
    public List<string>? ClientIds { get; set; }
}