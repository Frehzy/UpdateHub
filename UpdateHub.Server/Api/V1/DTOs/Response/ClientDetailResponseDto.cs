namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class ClientDetailResponseDto : ClientResponseDto
{
    public string? UserAgent { get; set; }
    public string? CpuInfo { get; set; }
    public int? MemoryGb { get; set; }
    public int? DiskGb { get; set; }
    public string? Architecture { get; set; }
    public string? KernelVersion { get; set; }
    public string? BlockedReason { get; set; }
    public DateTime? BlockedAt { get; set; }
    public string? BlockedBy { get; set; }
    public List<ClientHistoryResponseDto>? History { get; set; }
}