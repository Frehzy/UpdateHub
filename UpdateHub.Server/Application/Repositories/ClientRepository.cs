using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

/// <summary>Доступ к компьютерам.</summary>
/// <param name="context">Контекст базы данных.</param>
public class ClientRepository(AppDbContext context)
    : BaseRepository<ClientEntity, string>(context), IClientRepository
{
    /// <inheritdoc />
    public Task<ClientEntity?> GetByIdWithDetailsAsync(string clientId, CancellationToken cancellationToken = default)
        => Set
            .Include(c => c.ComputerInfo)
            .Include(c => c.NetworkInfos)
            .Include(c => c.Group)
            .Include(c => c.BlockHistory)
            .FirstOrDefaultAsync(c => c.Id == clientId && c.IsActive, cancellationToken);

    /// <inheritdoc />
    public Task<ClientEntity?> GetActiveWithInfoAsync(string clientId, CancellationToken cancellationToken = default)
        => Set
            .Include(c => c.ComputerInfo)
            .FirstOrDefaultAsync(c => c.Id == clientId && c.IsActive, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClientEntity>> SearchAsync(
        string? groupId = null,
        bool? isBlocked = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = Set
            .Include(c => c.ComputerInfo)
            .Include(c => c.NetworkInfos)
            .Include(c => c.Group)
            .Where(c => c.IsActive);

        if (!string.IsNullOrEmpty(groupId))
        {
            query = query.Where(c => c.GroupId == groupId);
        }

        if (isBlocked.HasValue)
        {
            query = query.Where(c => c.IsBlocked == isBlocked.Value);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c =>
                c.Id.Contains(search) ||
                (c.ComputerInfo != null && c.ComputerInfo.Hostname.Contains(search)));
        }

        return await query.ToListAsync(cancellationToken);
    }
}
