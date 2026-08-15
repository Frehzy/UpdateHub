using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IClientBlockHistoryRepository : IRepository<ClientBlockHistoryEntity>
{
    Task<IEnumerable<ClientBlockHistoryEntity>> GetByClientIdAsync(string clientId);
}