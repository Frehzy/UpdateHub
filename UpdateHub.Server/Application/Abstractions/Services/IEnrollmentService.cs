using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Services;

/// <summary>Приём и рассмотрение заявок на регистрацию компьютеров.</summary>
public interface IEnrollmentService
{
    /// <summary>
    /// Принимает заявку от скрипта клиента.
    /// </summary>
    /// <param name="request">Сведения о компьютере.</param>
    /// <param name="remoteIpAddress">Адрес, с которого пришла заявка.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданная либо ранее поданная заявка.</returns>
    Task<EnrollmentRequestEntity> SubmitAsync(
        EnrollmentSubmission request,
        string? remoteIpAddress,
        CancellationToken cancellationToken = default);

    /// <summary>Одобряет заявку и заводит компьютер.</summary>
    /// <param name="requestId">Идентификатор заявки.</param>
    /// <param name="groupId">Группа, в которую поместить компьютер.</param>
    /// <param name="resolvedBy">Логин администратора.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданный компьютер.</returns>
    Task<ClientEntity> ApproveAsync(
        string requestId,
        string? groupId,
        string resolvedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Отклоняет заявку.</summary>
    /// <param name="requestId">Идентификатор заявки.</param>
    /// <param name="resolvedBy">Логин администратора.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task RejectAsync(string requestId, string resolvedBy, CancellationToken cancellationToken = default);
}

/// <summary>
/// Сведения о компьютере, присланные вместе с заявкой.
/// </summary>
/// <param name="ClientId">Идентификатор компьютера.</param>
/// <param name="HardwareFingerprint">Отпечаток железа.</param>
/// <param name="Hostname">Сетевое имя.</param>
/// <param name="OsVersion">Версия операционной системы.</param>
/// <param name="Username">Логин пользователя, подающего заявку.</param>
/// <param name="Comment">Комментарий пользователя.</param>
public sealed record EnrollmentSubmission(
    string ClientId,
    string? HardwareFingerprint,
    string? Hostname,
    string? OsVersion,
    string? Username,
    string? Comment);
