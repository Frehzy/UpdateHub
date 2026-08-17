using Microsoft.EntityFrameworkCore;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Clients;
using UpdateHub.BackendServer.Domain.Entities.Clients;
using UpdateHub.BackendServer.Infrastructure.Database;

namespace UpdateHub.BackendServer.Application.Repositories.Clients;

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
