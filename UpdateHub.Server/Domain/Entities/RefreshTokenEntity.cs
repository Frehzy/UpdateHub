namespace UpdateHub.Server.Domain.Entities;

public class RefreshTokenEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty; // Хеш refresh-токена
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? ClientIp { get; set; }
    public string? UserAgent { get; set; }

    public UserEntity? User { get; set; }
}