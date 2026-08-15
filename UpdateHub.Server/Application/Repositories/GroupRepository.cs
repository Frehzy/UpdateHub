using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class GroupRepository(AppDbContext context) : BaseRepository<GroupEntity>(context), IGroupRepository
{
    public async Task<GroupEntity?> GetByNameAsync(string name)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.Name == name && x.IsActive);
    }

    public async Task<IEnumerable<GroupEntity>> GetActiveGroupsAsync()
    {
        return await _dbSet.Where(x => x.IsActive).ToListAsync();
    }
}