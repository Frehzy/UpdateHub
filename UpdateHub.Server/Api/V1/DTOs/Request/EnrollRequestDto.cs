using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace UpdateHub.Server.Api.V1.DTOs.Request;

/// <summary>
/// Заявка на регистрацию компьютера, подаваемая без авторизации.
/// </summary>
/// <remarks>
/// Нужна потому, что сервер не заводит компьютеры сам. Пользователь,
/// который не смог войти из-за незарегистрированного компьютера,
/// отправляет эту заявку, и администратор видит её в панели управления.
/// </remarks>
public class EnrollRequestDto
{
    /// <summary>Идентификатор компьютера из <c>/etc/updatehub/client-id</c>.</summary>
    [Required(ErrorMessage = "Не указан идентификатор компьютера")]
    [FromForm(Name = "client_id")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Отпечаток железа.</summary>
    [FromForm(Name = "hardware_fingerprint")]
    public string? HardwareFingerprint { get; set; }

    /// <summary>Сетевое имя компьютера.</summary>
    [FromForm(Name = "hostname")]
    public string? Hostname { get; set; }

    /// <summary>Версия операционной системы.</summary>
    [FromForm(Name = "os_version")]
    public string? OsVersion { get; set; }

    /// <summary>Логин пользователя, который пытается работать за компьютером.</summary>
    [FromForm(Name = "username")]
    public string? Username { get; set; }

    /// <summary>Произвольный комментарий для администратора.</summary>
    [FromForm(Name = "comment")]
    public string? Comment { get; set; }
}
