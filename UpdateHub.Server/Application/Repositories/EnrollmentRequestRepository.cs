using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Domain.Enums;
using UpdateHub.Server.Infrastructure.Database;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Server.Application.Repositories;

/// <summary>Доступ к заявкам на регистрацию компьютеров.</summary>
/// <param name="context">Контекст базы данных.</param>
public class EnrollmentRequestRepository(AppDbContext context)
    : BaseRepository<EnrollmentRequestEntity, string>(context), IEnrollmentRequestRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<EnrollmentRequestEntity>> GetByStatusAsync(
        EnrollmentStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = status.HasValue ? Set.Where(x => x.Status == status.Value) : Set;
        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<EnrollmentRequestEntity?> GetPendingByClientIdAsync(
        string clientId,
        CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(
            x => x.ClientId == clientId && x.Status == EnrollmentStatus.Pending,
            cancellationToken);
}
