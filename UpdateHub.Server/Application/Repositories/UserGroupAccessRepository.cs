using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

/// <summary>Доступ к разрешениям на группы компьютеров.</summary>
/// <param name="context">Контекст базы данных.</param>
public class UserGroupAccessRepository(AppDbContext context)
    : BaseRepository<UserGroupAccessEntity, string>(context), IUserGroupAccessRepository
{
    /// <inheritdoc />
    public Task<bool> ExistsAsync(string userId, string groupId, CancellationToken cancellationToken = default)
        => Set.AnyAsync(x => x.UserId == userId && x.GroupId == groupId, cancellationToken);

    /// <inheritdoc />
    public Task<UserGroupAccessEntity?> GetAsync(string userId, string groupId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(x => x.UserId == userId && x.GroupId == groupId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserGroupAccessEntity>> GetByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
        => await Set.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
}
