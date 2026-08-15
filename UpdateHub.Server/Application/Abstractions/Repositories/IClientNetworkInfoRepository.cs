using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IClientNetworkInfoRepository : IRepository<ClientNetworkInfoEntity>
{
    Task<IEnumerable<ClientNetworkInfoEntity>> GetByClientIdAsync(string clientId);
    Task<ClientNetworkInfoEntity?> GetByClientAndIpAsync(string clientId, string ipAddress);
    Task<IEnumerable<ClientNetworkInfoEntity>> GetInactiveSinceAsync(DateTime cutoff);
}