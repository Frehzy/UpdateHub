namespace UpdateHub.Server.Domain.Entities;

public class ClientComputerInfoEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ClientId { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string? OsVersion { get; set; }
    public string? CpuInfo { get; set; }
    public int? MemoryGb { get; set; }
    public int? DiskGb { get; set; }
    public string? Architecture { get; set; }
    public string? KernelVersion { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ClientEntity? Client { get; set; }
}