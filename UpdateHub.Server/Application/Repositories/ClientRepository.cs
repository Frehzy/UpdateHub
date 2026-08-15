using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class ClientRepository(AppDbContext context) : BaseRepository<ClientEntity>(context), IClientRepository
{
    public async Task<ClientEntity?> GetByIdWithDetailsAsync(string id)
    {
        return await _dbSet
            .Include(c => c.ComputerInfo)
            .Include(c => c.NetworkInfos)
            .Include(c => c.Group)
            .Include(c => c.BlockHistory)
            .Include(c => c.History)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);
    }

    public async Task<IEnumerable<ClientEntity>> GetAllAsync(string? groupId = null, bool? isBlocked = null, string? search = null)
    {
        var query = _dbSet
            .Include(c => c.ComputerInfo)
            .Include(c => c.NetworkInfos)
            .Include(c => c.Group)
            .Where(c => c.IsActive);

        if (!string.IsNullOrEmpty(groupId))
            query = query.Where(c => c.GroupId == groupId);

        if (isBlocked.HasValue)
            query = query.Where(c => c.IsBlocked == isBlocked.Value);

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c =>
                c.Id.Contains(search) ||
                (c.ComputerInfo != null && c.ComputerInfo.Hostname.Contains(search)));
        }

        return await query.ToListAsync();
    }
}