using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Services;

public interface IManifestService
{
    Task RefreshManifestAsync(CancellationToken cancellationToken = default);
    Task UpdateManifestEntryAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<ManifestEntryEntity?> GetEntryByIdAsync(string id);
    Task<ManifestEntryEntity?> GetEntryByPathAsync(string relativePath);
    Task<IEnumerable<ManifestEntryEntity>> GetAllEntriesAsync();
    Task<bool> IsManifestUpdatingAsync();
    Task<string> ComputeMd5Async(string filePath, CancellationToken cancellationToken = default);
    string GetFilesPath();
}