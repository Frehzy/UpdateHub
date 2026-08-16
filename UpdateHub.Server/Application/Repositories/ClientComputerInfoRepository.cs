using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

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
