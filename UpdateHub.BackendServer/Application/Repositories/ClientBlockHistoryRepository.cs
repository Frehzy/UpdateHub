using Microsoft.EntityFrameworkCore;
using UpdateHub.BackendServer.Application.Abstractions.Repositories;
using UpdateHub.BackendServer.Domain.Entities;
using UpdateHub.BackendServer.Infrastructure.Database;

namespace UpdateHub.BackendServer.Application.Repositories;

/// <summary>Доступ к истории блокировок компьютеров.</summary>
/// <param name="context">Контекст базы данных.</param>
public class ClientBlockHistoryRepository(AppDbContext context)
    : BaseRepository<ClientBlockHistoryEntity, string>(context), IClientBlockHistoryRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ClientBlockHistoryEntity>> GetByClientIdAsync(
        string clientId,
        CancellationToken cancellationToken = default)
        => await Set
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<string?> GetLatestBlockReasonAsync(string clientId, CancellationToken cancellationToken = default)
        => Set
            .Where(x => x.ClientId == clientId && x.Action == "blocked")
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Reason)
            .FirstOrDefaultAsync(cancellationToken);
}
