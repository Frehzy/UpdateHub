using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IUserRepository : IRepository<UserEntity>
{
    Task<UserEntity?> GetByUsernameAsync(string username);
    Task<IEnumerable<UserEntity>> GetByRoleAsync(string role);
}