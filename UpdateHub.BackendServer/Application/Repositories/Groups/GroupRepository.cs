using Microsoft.EntityFrameworkCore;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Groups;
using UpdateHub.BackendServer.Application.Abstractions.Repositories;
using UpdateHub.BackendServer.Domain.Entities.Groups;
using UpdateHub.BackendServer.Infrastructure.Database;

namespace UpdateHub.BackendServer.Application.Repositories.Groups;

/// <summary>Доступ к группам компьютеров.</summary>
/// <param name="context">Контекст базы данных.</param>
public class GroupRepository(AppDbContext context)
    : BaseRepository<GroupEntity, string>(context), IGroupRepository
{
    /// <inheritdoc />
    public Task<GroupEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(x => x.Name == name && x.IsActive, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupEntity>> GetActiveAsync(CancellationToken cancellationToken = default)
        => await Set.Include(g => g.Clients).Where(x => x.IsActive).ToListAsync(cancellationToken);
}
