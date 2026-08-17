using Microsoft.EntityFrameworkCore;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Updates;
using UpdateHub.BackendServer.Application.Abstractions.Repositories;
using UpdateHub.BackendServer.Domain.Entities.Updates;
using UpdateHub.BackendServer.Infrastructure.Database;

namespace UpdateHub.BackendServer.Application.Repositories.Updates;

/// <summary>Доступ к пофайловой детализации обращений.</summary>
/// <param name="context">Контекст базы данных.</param>
public class UpdateDetailRepository(AppDbContext context)
    : BaseRepository<UpdateDetailEntity, int>(context), IUpdateDetailRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<UpdateDetailEntity>> GetByRequestIdAsync(
        int updateRequestId,
        CancellationToken cancellationToken = default)
        => await Set.Where(x => x.UpdateRequestId == updateRequestId).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddRangeAsync(
        IReadOnlyCollection<UpdateDetailEntity> details,
        CancellationToken cancellationToken = default)
    {
        if (details.Count == 0)
        {
            return;
        }

        await Set.AddRangeAsync(details, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
    }
}
