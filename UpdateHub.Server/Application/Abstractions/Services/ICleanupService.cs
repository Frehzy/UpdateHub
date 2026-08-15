namespace UpdateHub.Server.Application.Abstractions.Services;

public interface ICleanupService
{
    Task CleanupAsync(CancellationToken cancellationToken = default);
}