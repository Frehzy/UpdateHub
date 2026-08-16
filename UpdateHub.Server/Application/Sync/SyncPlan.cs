using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Application.Sync;

/// <summary>
/// План синхронизации, отдаваемый клиенту.
/// </summary>
/// <param name="Status">Итог сравнения.</param>
/// <param name="Generation">Поколение манифеста на момент сравнения.</param>
/// <param name="FilesToDownload">Файлы, которые клиенту нужно скачать.</param>
/// <param name="ExtraFiles">
/// Файлы, которые есть у клиента, но отсутствуют на сервере.
/// Клиент их не удаляет — только показывает пользователю.
/// </param>
public sealed record SyncPlan(
    UpdateStatus Status,
    long Generation,
    IReadOnlyList<SyncFile> FilesToDownload,
    IReadOnlyList<string> ExtraFiles)
{
    /// <summary>Суммарный объём файлов к скачиванию в байтах.</summary>
    public long TotalSizeBytes => FilesToDownload.Sum(f => f.SizeBytes);
}

/// <summary>
/// Файл, подлежащий скачиванию.
/// </summary>
/// <param name="ManifestEntryId">Идентификатор записи манифеста для журнала.</param>
/// <param name="RelativePath">Путь относительно каталога раздачи.</param>
/// <param name="Md5Hash">Ожидаемая контрольная сумма.</param>
/// <param name="SizeBytes">Размер в байтах.</param>
/// <param name="ClientMd5Hash">Сумма, которая была у клиента; пусто, если файла не было.</param>
public sealed record SyncFile(
    string ManifestEntryId,
    string RelativePath,
    string Md5Hash,
    long SizeBytes,
    string? ClientMd5Hash);
