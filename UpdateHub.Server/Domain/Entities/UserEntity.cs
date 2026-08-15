using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Domain.Entities;

public class UserEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLogin { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; } = false;

    // Navigation properties
    public ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = [];
    public ICollection<UserClientAccessEntity> UserClientAccesses { get; set; } = [];
    public ICollection<UserGroupAccessEntity> UserGroupAccesses { get; set; } = [];
    public ICollection<ClientSessionEntity> ClientSessions { get; set; } = [];
}