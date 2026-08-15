namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class ErrorResponseDto
{
    public string Error { get; set; } = string.Empty;
    public string? Reason { get; set; }
}