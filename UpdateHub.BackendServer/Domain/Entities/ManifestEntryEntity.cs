namespace UpdateHub.BackendServer.Domain.Entities;

/// <summary>
/// Запись эталонного манифеста — один файл в каталоге раздачи.
/// </summary>
/// <remarks>
/// <see cref="SizeBytes"/> и <see cref="LastModified"/> хранятся не только для
/// отчётности: сканер сравнивает их при каждом обходе и пересчитывает
/// <see cref="Md5Hash"/> только у изменившихся файлов. Без этого чтение
/// шестигигабайтного образа через проброшенную папку повторялось бы
/// при каждом опросе.
/// </remarks>
public class ManifestEntryEntity
{
    /// <summary>Первичный ключ.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Путь относительно каталога раздачи, разделители — прямые слэши.
    /// Он же служит адресом файла при скачивании.
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>Контрольная сумма MD5 в нижнем регистре, 32 шестнадцатеричных символа.</summary>
    public string Md5Hash { get; set; } = string.Empty;

    /// <summary>Размер файла в байтах.</summary>
    public long SizeBytes { get; set; }

    /// <summary>Время последнего изменения файла на диске (UTC).</summary>
    public DateTime LastModified { get; set; }

    /// <summary>Момент появления файла в манифесте.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Момент последнего обновления записи.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Записи о выдачах этого файла клиентам.</summary>
    public ICollection<UpdateDetailEntity> UpdateDetails { get; set; } = [];

    /// <summary>История изменений файла.</summary>
    public ICollection<FileChangeEntity> FileChanges { get; set; } = [];
}
