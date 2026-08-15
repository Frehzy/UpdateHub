using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class ClientNetworkInfoRepository(AppDbContext context) : BaseRepository<ClientNetworkInfoEntity>(context), IClientNetworkInfoRepository
{
    public async Task<IEnumerable<ClientNetworkInfoEntity>> GetByClientIdAsync(string clientId)
    {
        return await _dbSet.Where(x => x.ClientId == clientId).ToListAsync();
    }

    public async Task<ClientNetworkInfoEntity?> GetByClientAndIpAsync(string clientId, string ipAddress)
    {
        return await _dbSet
            .FirstOrDefaultAsync(x => x.ClientId == clientId && x.IpAddress == ipAddress);
    }

    public async Task<IEnumerable<ClientNetworkInfoEntity>> GetInactiveSinceAsync(DateTime cutoff)
    {
        return await _dbSet
            .Where(x => x.LastSeen < cutoff && x.IsActive)
            .ToListAsync();
    }
}