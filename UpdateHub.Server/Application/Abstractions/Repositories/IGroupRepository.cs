using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IGroupRepository : IRepository<GroupEntity>
{
    Task<GroupEntity?> GetByNameAsync(string name);
    Task<IEnumerable<GroupEntity>> GetActiveGroupsAsync();
}