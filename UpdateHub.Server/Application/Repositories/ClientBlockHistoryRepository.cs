using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class ClientBlockHistoryRepository(AppDbContext context) : BaseRepository<ClientBlockHistoryEntity>(context), IClientBlockHistoryRepository
{
    public async Task<IEnumerable<ClientBlockHistoryEntity>> GetByClientIdAsync(string clientId)
    {
        return await _dbSet
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }
}