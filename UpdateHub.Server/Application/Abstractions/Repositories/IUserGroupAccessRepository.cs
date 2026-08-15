using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IUserGroupAccessRepository : IRepository<UserGroupAccessEntity>
{
    Task<UserGroupAccessEntity?> GetByUserAndGroupAsync(string userId, string groupId);
    Task<IEnumerable<UserGroupAccessEntity>> GetByUserIdAsync(string userId);
    Task<IEnumerable<UserGroupAccessEntity>> GetByGroupIdAsync(string groupId);
}