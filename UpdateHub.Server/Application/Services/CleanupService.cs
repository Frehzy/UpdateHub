using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;

namespace UpdateHub.Server.Application.Services;

public class CleanupService(
    IUpdateRequestRepository updateRequestRepository,
    IUpdateDetailRepository updateDetailRepository,
    IFileChangeRepository fileChangeRepository,
    IClientHistoryRepository clientHistoryRepository,
    IClientNetworkInfoRepository clientNetworkInfoRepository,
    ILogger<CleanupService> logger) : BackgroundService, ICleanupService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CleanupService starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Запускаем очистку раз в сутки в 3:00
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1).AddHours(3);
                var delay = nextRun - now;

                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.FromHours(24);
                }

                await Task.Delay(delay, stoppingToken);
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "CleanupService error");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        logger.LogInformation("CleanupService stopped");
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting cleanup");

        try
        {
            // Удаляем UpdateRequests старше 30 дней
            var cutoff30Days = DateTime.UtcNow.AddDays(-30);
            var oldRequests = await updateRequestRepository.GetOlderThanAsync(cutoff30Days);

            foreach (var request in oldRequests)
            {
                await updateRequestRepository.DeleteAsync(request.Id.ToString());
            }

            logger.LogInformation("Deleted {Count} old update requests", oldRequests.Count());

            // Удаляем FileChanges старше 90 дней
            var cutoff90Days = DateTime.UtcNow.AddDays(-90);
            var oldChanges = await fileChangeRepository.GetOlderThanAsync(cutoff90Days);

            foreach (var change in oldChanges)
            {
                await fileChangeRepository.DeleteAsync(change.Id.ToString());
            }

            logger.LogInformation("Deleted {Count} old file changes", oldChanges.Count());

            // Удаляем ClientHistory старше 180 дней
            var cutoff180Days = DateTime.UtcNow.AddDays(-180);
            var oldHistory = await clientHistoryRepository.GetOlderThanAsync(cutoff180Days);

            foreach (var history in oldHistory)
            {
                await clientHistoryRepository.DeleteAsync(history.Id.ToString());
            }

            logger.LogInformation("Deleted {Count} old client history records", oldHistory.Count());

            // Деактивируем старые сетевые записи (старше 30 дней без обновления)
            var cutoff30DaysNetwork = DateTime.UtcNow.AddDays(-30);
            var oldNetwork = await clientNetworkInfoRepository.GetInactiveSinceAsync(cutoff30DaysNetwork);

            foreach (var network in oldNetwork)
            {
                network.IsActive = false;
                await clientNetworkInfoRepository.UpdateAsync(network);
            }

            logger.LogInformation("Deactivated {Count} old network records", oldNetwork.Count());

            logger.LogInformation("Cleanup completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cleanup failed");
            throw;
        }
    }
}