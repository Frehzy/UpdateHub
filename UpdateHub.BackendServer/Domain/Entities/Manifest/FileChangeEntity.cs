using UpdateHub.BackendServer.Domain.Enums;

namespace UpdateHub.BackendServer.Domain.Entities.Manifest;

/// <summary>
/// Запись об изменении файла в каталоге раздачи, обнаруженном сканером.
/// </summary>
public class FileChangeEntity
{
    /// <summary>Первичный ключ (автоинкремент).</summary>
    public int Id { get; set; }

    /// <summary>Запись манифеста. Обнуляется при удалении файла.</summary>
    public string? ManifestEntryId { get; set; }

    /// <summary>Путь файла относительно каталога раздачи.</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Характер изменения.</summary>
    public FileChangeType ChangeType { get; set; }

    /// <summary>Прежняя контрольная сумма.</summary>
    public string? OldMd5Hash { get; set; }

    /// <summary>Новая контрольная сумма.</summary>
    public string? NewMd5Hash { get; set; }

    /// <summary>Момент обнаружения изменения.</summary>
    public DateTime ChangeTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Запись манифеста (навигационное свойство).</summary>
    public ManifestEntryEntity? ManifestEntry { get; set; }
}
