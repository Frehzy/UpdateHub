using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class ManifestEntryRepository(AppDbContext context) : BaseRepository<ManifestEntryEntity>(context), IManifestEntryRepository
{
    public async Task<ManifestEntryEntity?> GetByPathAsync(string relativePath)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.RelativePath == relativePath);
    }

    public async new Task<IEnumerable<ManifestEntryEntity>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }
}