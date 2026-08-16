using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Application.Sync;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Application.Services;

/// <summary>Журналирование обращений и сводная статистика.</summary>
/// <param name="updateRequestRepository">Доступ к журналу обращений.</param>
/// <param name="updateDetailRepository">Доступ к пофайловой детализации.</param>
/// <param name="logger">Журнал.</param>
public class StatisticsService(
    IUpdateRequestRepository updateRequestRepository,
    IUpdateDetailRepository updateDetailRepository,
    ILogger<StatisticsService> logger) : IStatisticsService
{
    /// <inheritdoc />
    public async Task<StatsResponseDto> GetStatisticsAsync(int? days, CancellationToken cancellationToken = default)
    {
        DateTime? from = days.HasValue ? DateTime.UtcNow.AddDays(-days.Value) : null;

        // Агрегаты считаются запросом к базе. Прежняя версия выгружала всю
        // таблицу обращений в память и вдобавок читала всех клиентов в переменную,
        // которая нигде не использовалась.
        var summary = await updateRequestRepository.GetSummaryAsync(from, cancellationToken);
        var daily = await updateRequestRepository.GetDailyCountsAsync(from, cancellationToken);

        return new StatsResponseDto
        {
            TotalRequests = summary.TotalRequests,
            UniqueClients = summary.UniqueClients,
            TotalDownloadedBytes = summary.TotalBytes,
            RequestsByDay = [.. daily.Select(d => new StatsDayDto { Date = d.Date, Count = d.Count })]
        };
    }

    /// <inheritdoc />
    public async Task<UpdateRequestEntity> LogSyncAsync(
        SyncPlan plan,
        SyncRequest request,
        int responseTimeMs,
        CancellationToken cancellationToken = default)
    {
        var entity = new UpdateRequestEntity
        {
            ClientId = request.ClientId,
            Username = request.Username,
            RequestTimestamp = DateTime.UtcNow,
            RequestType = request.RequestType,
            ClientManifestHash = ComputeManifestHash(request.ClientManifest),
            Status = plan.Status,
            FilesToUpdate = plan.FilesToDownload.Count,
            TotalSizeBytes = plan.TotalSizeBytes,
            ResponseTimeMs = responseTimeMs
        };

        await updateRequestRepository.CreateAsync(entity, cancellationToken);

        if (request.RequestType == RequestType.Sync && plan.FilesToDownload.Count > 0)
        {
            var details = plan.FilesToDownload
                .Select(f => new UpdateDetailEntity
                {
                    UpdateRequestId = entity.Id,
                    ManifestEntryId = f.ManifestEntryId,
                    RelativePath = f.RelativePath,
                    OldMd5Hash = f.ClientMd5Hash,
                    NewMd5Hash = f.Md5Hash,
                    SizeBytes = f.SizeBytes
                })
                .ToList();

            await updateDetailRepository.AddRangeAsync(details, cancellationToken);
        }

        logger.LogDebug("Записано обращение {RequestId} компьютера {ClientId}", entity.Id, request.ClientId);
        return entity;
    }

    /// <summary>
    /// Вычисляет отпечаток манифеста клиента для журнала.
    /// </summary>
    /// <param name="manifest">Манифест клиента.</param>
    /// <returns>Шестнадцатеричный SHA-256 либо пустая строка для пустого манифеста.</returns>
    /// <remarks>
    /// Позволяет по журналу увидеть, что компьютер обращается с одним и тем же
    /// состоянием папки, не храня сам манифест.
    /// </remarks>
    private static string ComputeManifestHash(IReadOnlyDictionary<string, string> manifest)
    {
        if (manifest.Count == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        foreach (var pair in manifest.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key).Append(':').Append(pair.Value).Append(';');
        }

        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(builder.ToString()));

        return Convert.ToHexStringLower(bytes);
    }
}
