namespace UpdateHub.Server.Application.Abstractions.Services;

public interface IFileWatcherService
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}