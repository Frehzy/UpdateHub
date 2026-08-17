using Microsoft.EntityFrameworkCore;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Users;
using UpdateHub.BackendServer.Application.Abstractions.Repositories;
using UpdateHub.BackendServer.Domain.Entities.Users;
using UpdateHub.BackendServer.Infrastructure.Database;

namespace UpdateHub.BackendServer.Application.Repositories.Users;

/// <summary>Доступ к выданным refresh-токенам.</summary>
/// <param name="context">Контекст базы данных.</param>
public class RefreshTokenRepository(AppDbContext context)
    : BaseRepository<RefreshTokenEntity, string>(context), IRefreshTokenRepository
{
    /// <inheritdoc />
    /// <remarks>
    /// Читает без отслеживания намеренно. Отзыв токенов выполняется одним
    /// SQL-запросом через <c>ExecuteUpdate</c>, который меняет строки в базе,
    /// но ничего не знает о копиях, уже загруженных в контекст. Обычное чтение
    /// вернуло бы такую копию с устаревшим признаком отзыва, и отозванный
    /// токен продолжил бы считаться действующим.
    /// </remarks>
    public Task<RefreshTokenEntity?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Set.AsNoTracking().FirstOrDefaultAsync(x => x.Token == tokenHash, cancellationToken);

    /// <inheritdoc />
    public Task<int> RevokeAsync(string tokenHash, CancellationToken cancellationToken = default)
        => Set
            .Where(x => x.Token == tokenHash && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, (DateTime?)DateTime.UtcNow), cancellationToken);

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
