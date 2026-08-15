namespace UpdateHub.Server.Domain.Entities;

public class GroupEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<ClientEntity> Clients { get; set; } = [];
    public ICollection<UserGroupAccessEntity> UserGroupAccesses { get; set; } = [];
}