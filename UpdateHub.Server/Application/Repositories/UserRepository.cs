using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class UserRepository(AppDbContext context) : BaseRepository<UserEntity>(context), IUserRepository
{
    public async Task<UserEntity?> GetByUsernameAsync(string username)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.Username == username);
    }

    public async Task<IEnumerable<UserEntity>> GetByRoleAsync(string role)
    {
        return await _dbSet.Where(x => x.Role.ToString() == role && x.IsActive).ToListAsync();
    }
}