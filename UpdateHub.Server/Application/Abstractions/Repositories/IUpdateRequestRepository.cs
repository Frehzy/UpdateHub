using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IUpdateRequestRepository : IRepository<UpdateRequestEntity>
{
    Task<IEnumerable<UpdateRequestEntity>> GetByClientIdAsync(string clientId);
    Task<IEnumerable<UpdateRequestEntity>> GetOlderThanAsync(DateTime cutoff);
}