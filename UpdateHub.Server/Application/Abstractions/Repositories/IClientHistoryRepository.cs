using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IClientHistoryRepository : IRepository<ClientHistoryEntity>
{
    Task<IEnumerable<ClientHistoryEntity>> GetByClientIdAsync(string clientId, int limit = 50);
    Task<IEnumerable<ClientHistoryEntity>> GetOlderThanAsync(DateTime cutoff);
}