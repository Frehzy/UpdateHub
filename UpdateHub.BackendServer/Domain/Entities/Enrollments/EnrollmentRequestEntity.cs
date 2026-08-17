using UpdateHub.Shared.Enums;

namespace UpdateHub.BackendServer.Domain.Entities.Enrollments;

/// <summary>
/// Заявка на регистрацию компьютера, поданная скриптом клиента.
/// </summary>
/// <remarks>
/// Сервер никогда не заводит клиента самостоятельно: обращение с неизвестным
/// идентификатором отклоняется. Чтобы пользователь не оказался в тупике,
/// скрипт может отправить заявку на анонимный эндпоинт <c>/api/v1/enroll</c>,
/// а администратор потом привяжет компьютер к группе через панель управления.
/// </remarks>
public class EnrollmentRequestEntity
{
    /// <summary>Первичный ключ.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Идентификатор компьютера из <c>/etc/updatehub/client-id</c>.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Отпечаток железа: хэш от серийных номеров, MAC-адресов и модели.
    /// Позволяет узнать компьютер после переустановки системы, когда
    /// <see cref="ClientId"/> сменился, но машина осталась той же.
    /// </summary>
    public string? HardwareFingerprint { get; set; }

    /// <summary>Сетевое имя компьютера на момент подачи заявки.</summary>
    public string? Hostname { get; set; }

    /// <summary>Версия операционной системы.</summary>
    public string? OsVersion { get; set; }

    /// <summary>Логин пользователя, от имени которого подана заявка.</summary>
    public string? RequestedByUsername { get; set; }

    /// <summary>IP-адрес, с которого пришла заявка (берётся из соединения, не из тела запроса).</summary>
    public string? RemoteIpAddress { get; set; }

    /// <summary>Произвольный комментарий пользователя.</summary>
    public string? Comment { get; set; }

    /// <summary>Текущее состояние заявки.</summary>
    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Pending;

    /// <summary>Момент подачи заявки.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Момент рассмотрения заявки администратором.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>Логин администратора, рассмотревшего заявку.</summary>
    public string? ResolvedBy { get; set; }
}
