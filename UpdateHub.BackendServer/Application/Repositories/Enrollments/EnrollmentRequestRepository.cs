using Microsoft.EntityFrameworkCore;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Enrollments;
using UpdateHub.BackendServer.Domain.Entities.Enrollments;
using UpdateHub.BackendServer.Infrastructure.Database;
using UpdateHub.Shared.Enums;

namespace UpdateHub.BackendServer.Application.Repositories.Enrollments;

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
