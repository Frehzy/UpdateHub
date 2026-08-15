namespace UpdateHub.Server.Domain.Entities;

public class ClientNetworkInfoEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ClientId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string? MacAddress { get; set; }
    public string? NetworkInterface { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ClientEntity? Client { get; set; }
}