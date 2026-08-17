namespace UpdateHub.Shared.Contracts;

/// <summary>Заявка на регистрацию компьютера в панели управления.</summary>
public class EnrollmentResponseDto
{
    /// <summary>Идентификатор заявки.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Идентификатор компьютера.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Отпечаток железа.</summary>
    public string? HardwareFingerprint { get; set; }

    /// <summary>Сетевое имя компьютера.</summary>
    public string? Hostname { get; set; }

    /// <summary>Версия операционной системы.</summary>
    public string? OsVersion { get; set; }

    /// <summary>Логин пользователя, подавшего заявку.</summary>
    public string? RequestedByUsername { get; set; }

    /// <summary>Адрес, с которого пришла заявка.</summary>
    public string? RemoteIpAddress { get; set; }

    /// <summary>Комментарий пользователя.</summary>
    public string? Comment { get; set; }

    /// <summary>Состояние заявки.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Момент подачи.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Момент рассмотрения.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>Логин администратора, рассмотревшего заявку.</summary>
    public string? ResolvedBy { get; set; }

    /// <summary>
    /// Компьютеры с таким же отпечатком железа. Подсказка администратору:
    /// после переустановки системы идентификатор меняется, а машина остаётся прежней.
    /// </summary>
    public List<string>? MatchingClientIds { get; set; }
}
