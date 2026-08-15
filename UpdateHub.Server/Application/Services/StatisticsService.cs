using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Services;

public class StatisticsService(
    IUpdateRequestRepository updateRequestRepository,
    IUpdateDetailRepository updateDetailRepository,
    IClientRepository clientRepository,
    ILogger<StatisticsService> logger) : IStatisticsService
{
    public async Task<StatsResponseDto> GetStatisticsAsync(int? days = null)
    {
        var requests = await updateRequestRepository.GetAllAsync();
        var clients = await clientRepository.GetAllAsync();

        if (days.HasValue)
        {
            var cutoff = DateTime.UtcNow.AddDays(-days.Value);
            requests = requests.Where(r => r.RequestTimestamp >= cutoff);
        }

        var totalRequests = requests.Count();
        var uniqueClients = requests.Select(r => r.ClientId).Distinct().Count();
        var totalBytes = requests.Sum(r => r.TotalSizeBytes);

        return new StatsResponseDto
        {
            TotalRequests = totalRequests,
            UniqueClients = uniqueClients,
            TotalDownloadedBytes = totalBytes,
            RequestsByDay = [.. requests
                .GroupBy(r => r.RequestTimestamp.Date)
                .Select(g => new StatsDayDto
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(d => d.Date)]
        };
    }

    public async Task LogUpdateRequestAsync(
        string clientId,
        string requestType,
        string? clientManifestHash,
        string status,
        int filesToUpdate,
        long totalSizeBytes,
        int? responseTimeMs)
    {
        try
        {
            var entity = new UpdateRequestEntity
            {
                ClientId = clientId,
                RequestTimestamp = DateTime.UtcNow,
                RequestType = Enum.Parse<Domain.Enums.RequestType>(requestType, true),
                ClientManifestHash = clientManifestHash,
                Status = Enum.Parse<Domain.Enums.UpdateStatus>(status, true),
                FilesToUpdate = filesToUpdate,
                TotalSizeBytes = totalSizeBytes,
                ResponseTimeMs = responseTimeMs
            };

            await updateRequestRepository.CreateAsync(entity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log update request for client {ClientId}", clientId);
        }
    }

    public async Task LogUpdateDetailsAsync(
        int updateRequestId,
        string manifestEntryId,
        string relativePath,
        string? oldMd5Hash,
        string newMd5Hash,
        long sizeBytes)
    {
        try
        {
            var entity = new UpdateDetailEntity
            {
                UpdateRequestId = updateRequestId,
                ManifestEntryId = manifestEntryId,
                RelativePath = relativePath,
                OldMd5Hash = oldMd5Hash,
                NewMd5Hash = newMd5Hash,
                SizeBytes = sizeBytes
            };

            await updateDetailRepository.CreateAsync(entity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to log update details for request {RequestId}", updateRequestId);
        }
    }
}