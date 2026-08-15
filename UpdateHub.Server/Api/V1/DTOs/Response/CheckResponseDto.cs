namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class CheckResponseDto
{
    public string Status { get; set; } = string.Empty; // "ok" или "update"
    public List<FileUpdateInfoDto>? Files { get; set; }
    public List<string>? DeleteFiles { get; set; }
}