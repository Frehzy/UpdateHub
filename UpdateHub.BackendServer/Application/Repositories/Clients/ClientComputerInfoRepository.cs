using Microsoft.EntityFrameworkCore;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Clients;
using UpdateHub.BackendServer.Domain.Entities.Clients;
using UpdateHub.BackendServer.Infrastructure.Database;

namespace UpdateHub.BackendServer.Application.Repositories.Clients;

/// <summary>Доступ к сведениям о железе компьютеров.</summary>
/// <param name="context">Контекст базы данных.</param>
public class ClientComputerInfoRepository(AppDbContext context)
    : BaseRepository<ClientComputerInfoEntity, string>(context), IClientComputerInfoRepository
{
    /// <inheritdoc />
    public Task<ClientComputerInfoEntity?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(x => x.ClientId == clientId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClientComputerInfoEntity>> GetByFingerprintAsync(
        string fingerprint,
        CancellationToken cancellationToken = default)
        => await Set.Where(x => x.HardwareFingerprint == fingerprint).ToListAsync(cancellationToken);
}
