using UpdateHub.BackendServer.Domain.Entities.Enrollments;
using UpdateHub.Shared.Enums;

namespace UpdateHub.BackendServer.Application.Abstractions.Repositories.Enrollments;

/// <summary>Доступ к заявкам на регистрацию компьютеров.</summary>
public interface IEnrollmentRequestRepository : IRepository<EnrollmentRequestEntity, string>
{
    /// <summary>Возвращает заявки указанного состояния.</summary>
    /// <param name="status">Состояние заявок; <see langword="null"/> — все.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список заявок от новых к старым.</returns>
    Task<IReadOnlyList<EnrollmentRequestEntity>> GetByStatusAsync(EnrollmentStatus? status, CancellationToken cancellationToken = default);

    /// <summary>Ищет необработанную заявку по идентификатору компьютера.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Заявка либо <see langword="null"/>.</returns>
    Task<EnrollmentRequestEntity?> GetPendingByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
}
