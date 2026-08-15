namespace UpdateHub.Server.Domain.Entities;

public class UserGroupAccessEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string GroupId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public UserEntity? User { get; set; }
    public GroupEntity? Group { get; set; }
}