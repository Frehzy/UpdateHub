using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Domain.Entities;

public class ClientHistoryEntity
{
    public int Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public ClientChangeType ChangeType { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangeTimestamp { get; set; } = DateTime.UtcNow;
    public int? RequestId { get; set; }

    public ClientEntity? Client { get; set; }
    public UpdateRequestEntity? UpdateRequest { get; set; }
}