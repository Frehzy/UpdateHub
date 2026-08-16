using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

/// <summary>Доступ к сетевым адресам компьютеров.</summary>
/// <param name="context">Контекст базы данных.</param>
public class ClientNetworkInfoRepository(AppDbContext context)
    : BaseRepository<ClientNetworkInfoEntity, string>(context), IClientNetworkInfoRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ClientNetworkInfoEntity>> GetByClientIdAsync(
        string clientId,
        CancellationToken cancellationToken = default)
        => await Set.Where(x => x.ClientId == clientId).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ClientNetworkInfoEntity?> GetByClientAndIpAsync(
        string clientId,
        string ipAddress,
        CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(x => x.ClientId == clientId && x.IpAddress == ipAddress, cancellationToken);

    /// <inheritdoc />
    public Task<int> DeactivateOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
        => Set
            .Where(x => x.LastSeen < cutoff && x.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false), cancellationToken);
}
