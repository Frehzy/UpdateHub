using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class ClientHistoryRepository(AppDbContext context) : BaseRepository<ClientHistoryEntity>(context), IClientHistoryRepository
{
    public async Task<IEnumerable<ClientHistoryEntity>> GetByClientIdAsync(string clientId, int limit = 50)
    {
        return await _dbSet
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.ChangeTimestamp)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IEnumerable<ClientHistoryEntity>> GetOlderThanAsync(DateTime cutoff)
    {
        return await _dbSet
            .Where(x => x.ChangeTimestamp < cutoff)
            .ToListAsync();
    }
}