namespace UpdateHub.Server.Domain.Entities;

public class ClientBlockHistoryEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ClientId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // "blocked" или "unblocked"
    public string? Reason { get; set; }
    public string? BlockedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ClientEntity? Client { get; set; }
}