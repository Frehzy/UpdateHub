namespace UpdateHub.Shared.Contracts;

/// <summary>Состояние эталонного манифеста.</summary>
public class ManifestStatusResponseDto
{
    /// <summary>Поколение манифеста; растёт при каждом обнаруженном изменении.</summary>
    public long Generation { get; set; }

    /// <summary>Идёт ли обход каталога прямо сейчас.</summary>
    public bool IsScanning { get; set; }

    /// <summary>Момент завершения последнего обхода.</summary>
    public DateTime? LastScanCompletedAt { get; set; }

    /// <summary>Число файлов в манифесте.</summary>
    public int EntryCount { get; set; }

    /// <summary>Суммарный объём файлов в байтах.</summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>
    /// Пути, отвергнутые при обходе, с причиной. Сюда попадают недопустимые
    /// имена и конфликты регистра между NTFS на сервере и ext4 на клиентах.
    /// </summary>
    public IReadOnlyList<string> RejectedPaths { get; set; } = [];
}
