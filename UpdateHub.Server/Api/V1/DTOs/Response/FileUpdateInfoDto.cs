namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class FileUpdateInfoDto
{
    public string Id { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Md5Hash { get; set; } = string.Empty;
}