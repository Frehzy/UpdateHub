using Microsoft.Extensions.Options;
using System.Text;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Manifest;
using UpdateHub.BackendServer.Application.Abstractions.Services.Manifest;
using UpdateHub.BackendServer.Application.Manifest;
using UpdateHub.BackendServer.Domain.Entities.Manifest;
using UpdateHub.BackendServer.Infrastructure.Configuration;

namespace UpdateHub.BackendServer.Application.Services.Manifest;

/// <summary>Чтение эталонного манифеста.</summary>
/// <param name="config">Настройки раздачи.</param>
/// <param name="manifestEntryRepository">Доступ к записям манифеста.</param>
public class ManifestService(
    IOptions<UpdateHubConfig> config,
    IManifestEntryRepository manifestEntryRepository) : IManifestService
{
    private readonly UpdateHubConfig _config = config.Value;

    /// <inheritdoc />
    public Task<ManifestEntryEntity?> GetEntryAsync(string relativePath, CancellationToken cancellationToken = default)
        => manifestEntryRepository.GetByPathAsync(relativePath, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ManifestEntryEntity>> GetAllEntriesAsync(CancellationToken cancellationToken = default)
        => manifestEntryRepository.GetAllAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<string> RenderManifestAsync(CancellationToken cancellationToken = default)
    {
        var entries = await manifestEntryRepository.GetAllAsync(cancellationToken);
        var builder = new StringBuilder();

        foreach (var entry in entries.OrderBy(e => e.RelativePath, StringComparer.Ordinal))
        {
            ManifestFormat.AppendLine(builder, entry.Md5Hash, entry.RelativePath);
        }

        return builder.ToString();
    }

    /// <inheritdoc />
    public async Task<(ManifestEntryEntity Entry, string FullPath)?> ResolveFileAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var entry = await manifestEntryRepository.GetByPathAsync(relativePath, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        var root = _config.ResolvedFilesPath;
        var fullPath = Path.GetFullPath(Path.Combine(root, entry.RelativePath));

        // Путь пришёл из базы, куда попадает только результат обхода каталога,
        // поэтому выйти за его пределы он не может. Проверка оставлена как
        // страховка на случай, если содержимое базы окажется изменено вручную.
        if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        return File.Exists(fullPath) ? (entry, fullPath) : null;
    }
}
