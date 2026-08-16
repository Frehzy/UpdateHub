namespace UpdateHub.Server.Api.V1.DTOs.Response;

/// <summary>Подробные сведения о компьютере.</summary>
public class ClientDetailResponseDto : ClientResponseDto
{
    /// <summary>Отпечаток железа.</summary>
    public string? HardwareFingerprint { get; set; }

    /// <summary>Модель процессора.</summary>
    public string? CpuInfo { get; set; }

    /// <summary>Объём оперативной памяти в гигабайтах.</summary>
    public int? MemoryGb { get; set; }

    /// <summary>Объём диска в гигабайтах.</summary>
    public int? DiskGb { get; set; }

    /// <summary>Архитектура процессора.</summary>
    public string? Architecture { get; set; }

    /// <summary>Версия ядра.</summary>
    public string? KernelVersion { get; set; }

    /// <summary>Причина последней блокировки.</summary>
    public string? BlockedReason { get; set; }

    /// <summary>Момент последней блокировки.</summary>
    public DateTime? BlockedAt { get; set; }

    /// <summary>Логин администратора, выполнившего блокировку.</summary>
    public string? BlockedBy { get; set; }

    /// <summary>История изменений характеристик компьютера.</summary>
    public List<ClientHistoryResponseDto>? History { get; set; }
}
