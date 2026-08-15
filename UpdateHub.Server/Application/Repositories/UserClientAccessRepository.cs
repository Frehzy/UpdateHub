using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class UserClientAccessRepository(AppDbContext context) : BaseRepository<UserClientAccessEntity>(context), IUserClientAccessRepository
{
    public async Task<UserClientAccessEntity?> GetByUserAndClientAsync(string userId, string clientId)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.UserId == userId && x.ClientId == clientId);
    }

    public async Task<IEnumerable<UserClientAccessEntity>> GetByUserIdAsync(string userId)
    {
        return await _dbSet.Where(x => x.UserId == userId).ToListAsync();
    }

    public async Task<IEnumerable<UserClientAccessEntity>> GetByClientIdAsync(string clientId)
    {
        return await _dbSet.Where(x => x.ClientId == clientId).ToListAsync();
    }
}