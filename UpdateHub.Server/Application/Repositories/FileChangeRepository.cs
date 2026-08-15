using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class FileChangeRepository(AppDbContext context) : BaseRepository<FileChangeEntity>(context), IFileChangeRepository
{
    public async Task<IEnumerable<FileChangeEntity>> GetOlderThanAsync(DateTime cutoff)
    {
        return await _dbSet
            .Where(x => x.ChangeTimestamp < cutoff)
            .ToListAsync();
    }

    public async Task<IEnumerable<FileChangeEntity>> GetUnprocessedAsync()
    {
        return await _dbSet
            .Where(x => !x.IsProcessed)
            .ToListAsync();
    }
}