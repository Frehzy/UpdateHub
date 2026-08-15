using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class UserGroupAccessRepository(AppDbContext context) : BaseRepository<UserGroupAccessEntity>(context), IUserGroupAccessRepository
{
    public async Task<UserGroupAccessEntity?> GetByUserAndGroupAsync(string userId, string groupId)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.UserId == userId && x.GroupId == groupId);
    }

    public async Task<IEnumerable<UserGroupAccessEntity>> GetByUserIdAsync(string userId)
    {
        return await _dbSet.Where(x => x.UserId == userId).ToListAsync();
    }

    public async Task<IEnumerable<UserGroupAccessEntity>> GetByGroupIdAsync(string groupId)
    {
        return await _dbSet.Where(x => x.GroupId == groupId).ToListAsync();
    }
}