namespace UpdateHub.Server.Domain.Entities;

/// <summary>
/// Пофайловая детализация обращения: какой файл и с какой версии обновлялся.
/// </summary>
public class UpdateDetailEntity
{
    /// <summary>Первичный ключ (автоинкремент).</summary>
    public int Id { get; set; }

    /// <summary>Обращение, к которому относится запись.</summary>
    public int UpdateRequestId { get; set; }

    /// <summary>
    /// Запись манифеста, соответствующая файлу. Обнуляется, если файл
    /// впоследствии исчез из каталога раздачи, — сама запись о выдаче при этом сохраняется.
    /// </summary>
    public string? ManifestEntryId { get; set; }

    /// <summary>Путь файла относительно каталога раздачи.</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Контрольная сумма, которая была у клиента. Пусто, если файла у него не было.</summary>
    public string? OldMd5Hash { get; set; }

    /// <summary>Контрольная сумма на сервере.</summary>
    public string NewMd5Hash { get; set; } = string.Empty;

    /// <summary>Размер файла в байтах.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Обращение (навигационное свойство).</summary>
    public UpdateRequestEntity? UpdateRequest { get; set; }

    /// <summary>Запись манифеста (навигационное свойство).</summary>
    public ManifestEntryEntity? ManifestEntry { get; set; }
}
