using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class ClientSessionRepository(AppDbContext context) : BaseRepository<ClientSessionEntity>(context), IClientSessionRepository
{
    public async Task<IEnumerable<ClientSessionEntity>> GetByClientIdAsync(string clientId)
    {
        return await _dbSet
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.LoginAt)
            .ToListAsync();
    }

    public async Task<ClientSessionEntity?> GetActiveByClientIdAsync(string clientId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(x => x.ClientId == clientId && x.IsActive);
    }
}