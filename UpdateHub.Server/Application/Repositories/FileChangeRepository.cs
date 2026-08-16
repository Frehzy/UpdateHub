using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

/// <summary>Доступ к истории изменений файлов каталога раздачи.</summary>
/// <param name="context">Контекст базы данных.</param>
public class FileChangeRepository(AppDbContext context)
    : BaseRepository<FileChangeEntity, int>(context), IFileChangeRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<FileChangeEntity>> GetRecentAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
        => await Set
            .OrderByDescending(x => x.ChangeTimestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddRangeAsync(
        IReadOnlyCollection<FileChangeEntity> changes,
        CancellationToken cancellationToken = default)
    {
        if (changes.Count == 0)
        {
            return;
        }

        await Set.AddRangeAsync(changes, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
        => Set.Where(x => x.ChangeTimestamp < cutoff).ExecuteDeleteAsync(cancellationToken);
}
