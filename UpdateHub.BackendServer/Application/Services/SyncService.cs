using UpdateHub.BackendServer.Application.Abstractions.Repositories;
using UpdateHub.BackendServer.Application.Abstractions.Services;
using UpdateHub.BackendServer.Application.Manifest;
using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Domain.Enums;

namespace UpdateHub.BackendServer.Application.Services;

/// <summary>Сравнение манифеста клиента с эталонным.</summary>
/// <param name="manifestEntryRepository">Доступ к записям манифеста.</param>
/// <param name="state">Общее состояние манифеста.</param>
/// <param name="logger">Журнал.</param>
public class SyncService(
    IManifestEntryRepository manifestEntryRepository,
    ManifestState state,
    ILogger<SyncService> logger) : ISyncService
{
    /// <inheritdoc />
    public async Task<SyncPlan> BuildPlanAsync(SyncRequest request, CancellationToken cancellationToken = default)
    {
        var serverManifest = await manifestEntryRepository.GetAllByPathAsync(cancellationToken);
        var clientManifest = request.ClientManifest;

        var toDownload = new List<SyncFile>();

        foreach (var entry in serverManifest.Values)
        {
            var hasFile = clientManifest.TryGetValue(entry.RelativePath, out var clientMd5);

            if (hasFile && string.Equals(clientMd5, entry.Md5Hash, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            toDownload.Add(new SyncFile(
                entry.Id,
                entry.RelativePath,
                entry.Md5Hash,
                entry.SizeBytes,
                hasFile ? clientMd5 : null));
        }

        // Файлы, которых на сервере нет, клиент не удаляет — только сообщает
        // о них пользователю. Автоматическое удаление опасно: достаточно
        // отмонтировать каталог раздачи, чтобы приказать всем стереть свои данные.
        var extraFiles = clientManifest.Keys
            .Where(path => !serverManifest.ContainsKey(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var ordered = toDownload
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .ToList();

        var status = ordered.Count > 0 ? UpdateStatus.Update : UpdateStatus.Ok;

        logger.LogInformation(
            "Клиент {ClientId}: к скачиванию {Count} файлов ({Size} байт), лишних у клиента {Extra}",
            request.ClientId, ordered.Count, ordered.Sum(f => f.SizeBytes), extraFiles.Count);

        return new SyncPlan(status, state.Generation, ordered, extraFiles);
    }
}
