namespace UpdateHub.Server.Domain.Entities;

public class ClientEntity
{
    public string Id { get; set; } = string.Empty; // UUID клиента
    public string? GroupId { get; set; }
    public bool IsBlocked { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public GroupEntity? Group { get; set; }
    public ClientComputerInfoEntity? ComputerInfo { get; set; }
    public ICollection<ClientNetworkInfoEntity> NetworkInfos { get; set; } = [];
    public ICollection<ClientSessionEntity> Sessions { get; set; } = [];
    public ICollection<ClientBlockHistoryEntity> BlockHistory { get; set; } = [];
    public ICollection<ClientHistoryEntity> History { get; set; } = [];
    public ICollection<UserClientAccessEntity> UserClientAccesses { get; set; } = [];
    public ICollection<UpdateRequestEntity> UpdateRequests { get; set; } = [];
}