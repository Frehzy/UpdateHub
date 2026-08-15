namespace UpdateHub.Server.Domain.Entities;

public class ClientSessionEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ClientId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? SessionToken { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public DateTime LoginAt { get; set; } = DateTime.UtcNow;
    public DateTime? LogoutAt { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RefreshTokenId { get; set; }

    public ClientEntity? Client { get; set; }
    public UserEntity? User { get; set; }
}