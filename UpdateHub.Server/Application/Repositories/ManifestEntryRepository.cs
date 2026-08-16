using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

/// <summary>Доступ к записям эталонного манифеста.</summary>
/// <param name="context">Контекст базы данных.</param>
public class ManifestEntryRepository(AppDbContext context)
    : BaseRepository<ManifestEntryEntity, string>(context), IManifestEntryRepository
{
    /// <inheritdoc />
    public Task<ManifestEntryEntity?> GetByPathAsync(string relativePath, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(x => x.RelativePath == relativePath, cancellationToken);

    /// <inheritdoc />
    public Task<Dictionary<string, ManifestEntryEntity>> GetAllByPathAsync(CancellationToken cancellationToken = default)
        => Set.ToDictionaryAsync(x => x.RelativePath, x => x, StringComparer.Ordinal, cancellationToken);

    /// <inheritdoc />
    public Task<int> DeleteByPathsAsync(
        IReadOnlyCollection<string> relativePaths,
        CancellationToken cancellationToken = default)
    {
        if (relativePaths.Count == 0)
        {
            return Task.FromResult(0);
        }

        var paths = relativePaths.ToList();
        return Set.Where(x => paths.Contains(x.RelativePath)).ExecuteDeleteAsync(cancellationToken);
    }
}
