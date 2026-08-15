namespace UpdateHub.Server.Api.V1.DTOs.Request;

public class CheckRequestDto
{
    public ClientInfoDto? ClientInfo { get; set; }
    public Dictionary<string, string>? Manifest { get; set; }
}