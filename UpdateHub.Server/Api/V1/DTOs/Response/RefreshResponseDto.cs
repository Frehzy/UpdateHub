namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class RefreshResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}