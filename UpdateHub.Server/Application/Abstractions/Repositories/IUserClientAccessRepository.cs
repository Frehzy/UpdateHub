using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IUserClientAccessRepository : IRepository<UserClientAccessEntity>
{
    Task<UserClientAccessEntity?> GetByUserAndClientAsync(string userId, string clientId);
    Task<IEnumerable<UserClientAccessEntity>> GetByUserIdAsync(string userId);
    Task<IEnumerable<UserClientAccessEntity>> GetByClientIdAsync(string clientId);
}