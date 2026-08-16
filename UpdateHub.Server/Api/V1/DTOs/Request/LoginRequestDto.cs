using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace UpdateHub.Server.Api.V1.DTOs.Request;

/// <summary>
/// Данные для входа, присылаемые скриптом клиента.
/// </summary>
/// <remarks>
/// Передаются как обычная форма, чтобы на стороне bash хватало
/// <c>curl -d</c> без сборки JSON и без зависимости от <c>jq</c>.
/// Сведения о железе принимаются здесь же: это единственный запрос,
/// который скрипт выполняет гарантированно.
/// </remarks>
public class LoginRequestDto
{
    /// <summary>Логин пользователя.</summary>
    [Required(ErrorMessage = "Не указан логин")]
    [FromForm(Name = "username")]
    public string Username { get; set; } = string.Empty;

    /// <summary>Пароль пользователя.</summary>
    [Required(ErrorMessage = "Не указан пароль")]
    [FromForm(Name = "password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Идентификатор компьютера из <c>/etc/updatehub/client-id</c>.</summary>
    [Required(ErrorMessage = "Не указан идентификатор компьютера")]
    [FromForm(Name = "client_id")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Сетевое имя компьютера.</summary>
    [FromForm(Name = "hostname")]
    public string? Hostname { get; set; }

    /// <summary>Отпечаток железа — хэш от серийных номеров и модели.</summary>
    [FromForm(Name = "hardware_fingerprint")]
    public string? HardwareFingerprint { get; set; }

    /// <summary>Версия операционной системы.</summary>
    [FromForm(Name = "os_version")]
    public string? OsVersion { get; set; }

    /// <summary>Версия ядра.</summary>
    [FromForm(Name = "kernel_version")]
    public string? KernelVersion { get; set; }

    /// <summary>Архитектура процессора.</summary>
    [FromForm(Name = "architecture")]
    public string? Architecture { get; set; }

    /// <summary>Модель процессора.</summary>
    [FromForm(Name = "cpu_info")]
    public string? CpuInfo { get; set; }

    /// <summary>Объём оперативной памяти в гигабайтах.</summary>
    [FromForm(Name = "memory_gb")]
    public int? MemoryGb { get; set; }

    /// <summary>Объём диска в гигабайтах.</summary>
    [FromForm(Name = "disk_gb")]
    public int? DiskGb { get; set; }

    /// <summary>MAC-адрес основного сетевого интерфейса.</summary>
    [FromForm(Name = "mac_address")]
    public string? MacAddress { get; set; }
}
