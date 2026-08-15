using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IUpdateDetailRepository : IRepository<UpdateDetailEntity>
{
    Task<IEnumerable<UpdateDetailEntity>> GetByUpdateRequestIdAsync(int updateRequestId);
}