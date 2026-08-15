using UpdateHub.Server.Api.V1.DTOs.Response;

namespace UpdateHub.Server.Application.Abstractions.Services;

public interface IStatisticsService
{
    Task<StatsResponseDto> GetStatisticsAsync(int? days = null);
    Task LogUpdateRequestAsync(
        string clientId,
        string requestType,
        string? clientManifestHash,
        string status,
        int filesToUpdate,
        long totalSizeBytes,
        int? responseTimeMs);
    Task LogUpdateDetailsAsync(
        int updateRequestId,
        string manifestEntryId,
        string relativePath,
        string? oldMd5Hash,
        string newMd5Hash,
        long sizeBytes);
}