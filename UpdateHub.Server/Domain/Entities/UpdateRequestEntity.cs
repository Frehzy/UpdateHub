using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Domain.Entities;

public class UpdateRequestEntity
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;
    public RequestType RequestType { get; set; }
    public string? ClientManifestHash { get; set; }
    public UpdateStatus Status { get; set; }
    public int FilesToUpdate { get; set; } = 0;
    public long TotalSizeBytes { get; set; } = 0;
    public int? ResponseTimeMs { get; set; }

    public ClientEntity? Client { get; set; }
    public ICollection<UpdateDetailEntity> UpdateDetails { get; set; } = [];
    public ICollection<ClientHistoryEntity> ClientHistories { get; set; } = [];
}