using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

/// <summary>Доступ к выданным refresh-токенам.</summary>
/// <param name="context">Контекст базы данных.</param>
public class RefreshTokenRepository(AppDbContext context)
    : BaseRepository<RefreshTokenEntity, string>(context), IRefreshTokenRepository
{
    /// <inheritdoc />
    public Task<RefreshTokenEntity?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(x => x.Token == tokenHash, cancellationToken);

    /// <inheritdoc />
    public Task<int> RevokeAllForUserAsync(string userId, CancellationToken cancellationToken = default)
        => Set
            .Where(x => x.UserId == userId && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, (DateTime?)DateTime.UtcNow), cancellationToken);

    /// <inheritdoc />
    public Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default)
        => Set
            .Where(x => x.ExpiresAt < DateTime.UtcNow || x.RevokedAt != null)
            .ExecuteDeleteAsync(cancellationToken);
}
