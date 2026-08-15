using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class UpdateRequestRepository(AppDbContext context) : BaseRepository<UpdateRequestEntity>(context), IUpdateRequestRepository
{
    public async Task<IEnumerable<UpdateRequestEntity>> GetByClientIdAsync(string clientId)
    {
        return await _dbSet
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.RequestTimestamp)
            .ToListAsync();
    }

    public async Task<IEnumerable<UpdateRequestEntity>> GetOlderThanAsync(DateTime cutoff)
    {
        return await _dbSet
            .Where(x => x.RequestTimestamp < cutoff)
            .ToListAsync();
    }
}