using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IClientSessionRepository : IRepository<ClientSessionEntity>
{
    Task<IEnumerable<ClientSessionEntity>> GetByClientIdAsync(string clientId);
    Task<ClientSessionEntity?> GetActiveByClientIdAsync(string clientId);
}