using Microsoft.EntityFrameworkCore;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Manifest;
using UpdateHub.BackendServer.Application.Abstractions.Repositories;
using UpdateHub.BackendServer.Domain.Entities.Manifest;
using UpdateHub.BackendServer.Domain.ValueObjects;
using UpdateHub.BackendServer.Infrastructure.Database;

namespace UpdateHub.BackendServer.Application.Repositories.Manifest;

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
