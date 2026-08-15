namespace UpdateHub.Server.Api.V1.DTOs.Request;

public class ClientInfoDto
{
    public string ClientId { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string? OsVersion { get; set; }
    public string? CpuInfo { get; set; }
    public int? MemoryGb { get; set; }
    public int? DiskGb { get; set; }
    public string? Architecture { get; set; }
    public string? KernelVersion { get; set; }
}