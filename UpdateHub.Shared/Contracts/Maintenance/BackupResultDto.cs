namespace UpdateHub.Shared.Contracts.Maintenance;

/// <summary>
/// Итог снятия резервной копии базы.
/// </summary>
public class BackupResultDto
{
    /// <summary>Удалось ли снять копию.</summary>
    public bool Created { get; set; }

    /// <summary>Полный путь к файлу копии.</summary>
    public string? Path { get; set; }

    /// <summary>Размер копии в байтах.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Сообщение для человека.</summary>
    public string Message { get; set; } = string.Empty;
}
