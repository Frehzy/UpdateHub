namespace UpdateHub.Server.Domain.Entities;

public class UserClientAccessEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserEntity? User { get; set; }
    public ClientEntity? Client { get; set; }
}