using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

/// <summary>Доступ к персональным разрешениям на компьютеры.</summary>
/// <param name="context">Контекст базы данных.</param>
public class UserClientAccessRepository(AppDbContext context)
    : BaseRepository<UserClientAccessEntity, string>(context), IUserClientAccessRepository
{
    /// <inheritdoc />
    public Task<bool> ExistsAsync(string userId, string clientId, CancellationToken cancellationToken = default)
        => Set.AnyAsync(x => x.UserId == userId && x.ClientId == clientId, cancellationToken);

    /// <inheritdoc />
    public Task<UserClientAccessEntity?> GetAsync(string userId, string clientId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(x => x.UserId == userId && x.ClientId == clientId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserClientAccessEntity>> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
        => await Set.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
}
