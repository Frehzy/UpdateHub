using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Domain.Enums;
using UpdateHub.Server.Infrastructure.Configuration;

namespace UpdateHub.Server.Application.Services;

public class ManifestService(
    IOptions<UpdateHubConfig> config,
    IManifestEntryRepository manifestEntryRepository,
    IFileChangeRepository fileChangeRepository,
    ILogger<ManifestService> logger) : IManifestService
{
    private readonly UpdateHubConfig _config = config.Value;
    private readonly SemaphoreSlim _updateLock = new(1, 1);
    private bool _isUpdating = false;

    public async Task RefreshManifestAsync(CancellationToken cancellationToken = default)
    {
        if (!await _updateLock.WaitAsync(0, cancellationToken))
        {
            logger.LogWarning("Manifest refresh already in progress");
            return;
        }

        try
        {
            _isUpdating = true;
            logger.LogInformation("Starting full manifest refresh");

            var filesPath = _config.FilesPath;
            if (!Directory.Exists(filesPath))
            {
                logger.LogWarning("Files path does not exist: {FilesPath}", filesPath);
                Directory.CreateDirectory(filesPath);
            }

            // Получаем все файлы рекурсивно
            var files = Directory.GetFiles(filesPath, "*", SearchOption.AllDirectories);
            var existingEntries = await manifestEntryRepository.GetAllAsync();
            var existingDict = existingEntries.ToDictionary(e => e.RelativePath);

            var processedPaths = new HashSet<string>();

            foreach (var filePath in files)
            {
                var relativePath = Path.GetRelativePath(filesPath, filePath).Replace('\\', '/');

                try
                {
                    var md5 = await ComputeMd5Async(filePath, cancellationToken);
                    var fileInfo = new FileInfo(filePath);

                    if (existingDict.TryGetValue(relativePath, out var existingEntry))
                    {
                        // Обновляем существующую запись
                        if (existingEntry.Md5Hash != md5 || existingEntry.SizeBytes != fileInfo.Length)
                        {
                            var oldMd5 = existingEntry.Md5Hash;
                            existingEntry.Md5Hash = md5;
                            existingEntry.SizeBytes = fileInfo.Length;
                            existingEntry.LastModified = fileInfo.LastWriteTimeUtc;
                            existingEntry.UpdatedAt = DateTime.UtcNow;
                            await manifestEntryRepository.UpdateAsync(existingEntry);

                            // Записываем изменение
                            await fileChangeRepository.CreateAsync(new FileChangeEntity
                            {
                                ManifestEntryId = existingEntry.Id,
                                RelativePath = relativePath,
                                ChangeType = FileChangeType.Modified,
                                OldMd5Hash = oldMd5,
                                NewMd5Hash = md5,
                                ChangeTimestamp = DateTime.UtcNow,
                                IsProcessed = true
                            });

                            logger.LogDebug("Updated manifest entry: {Path}", relativePath);
                        }
                    }
                    else
                    {
                        // Создаём новую запись
                        var newEntry = new ManifestEntryEntity
                        {
                            RelativePath = relativePath,
                            Md5Hash = md5,
                            SizeBytes = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTimeUtc,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        await manifestEntryRepository.CreateAsync(newEntry);

                        await fileChangeRepository.CreateAsync(new FileChangeEntity
                        {
                            ManifestEntryId = newEntry.Id,
                            RelativePath = relativePath,
                            ChangeType = FileChangeType.Created,
                            NewMd5Hash = md5,
                            ChangeTimestamp = DateTime.UtcNow,
                            IsProcessed = true
                        });

                        logger.LogDebug("Added new manifest entry: {Path}", relativePath);
                    }

                    processedPaths.Add(relativePath);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process file: {Path}", relativePath);
                }
            }

            // Удаляем записи, которых больше нет
            foreach (var entry in existingDict.Values)
            {
                if (!processedPaths.Contains(entry.RelativePath))
                {
                    await manifestEntryRepository.DeleteAsync(entry.Id);

                    await fileChangeRepository.CreateAsync(new FileChangeEntity
                    {
                        ManifestEntryId = entry.Id,
                        RelativePath = entry.RelativePath,
                        ChangeType = FileChangeType.Deleted,
                        OldMd5Hash = entry.Md5Hash,
                        ChangeTimestamp = DateTime.UtcNow,
                        IsProcessed = true
                    });

                    logger.LogDebug("Removed manifest entry: {Path}", entry.RelativePath);
                }
            }

            logger.LogInformation("Manifest refresh completed. Total entries: {Count}", processedPaths.Count);
        }
        finally
        {
            _isUpdating = false;
            _updateLock.Release();
        }
    }

    public async Task UpdateManifestEntryAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var filesPath = _config.FilesPath;
        var fullPath = Path.Combine(filesPath, relativePath);

        try
        {
            var existing = await manifestEntryRepository.GetByPathAsync(relativePath);

            if (File.Exists(fullPath))
            {
                var md5 = await ComputeMd5Async(fullPath, cancellationToken);
                var fileInfo = new FileInfo(fullPath);

                if (existing != null)
                {
                    // Обновляем существующую запись
                    var oldMd5 = existing.Md5Hash;
                    existing.Md5Hash = md5;
                    existing.SizeBytes = fileInfo.Length;
                    existing.LastModified = fileInfo.LastWriteTimeUtc;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await manifestEntryRepository.UpdateAsync(existing);

                    if (oldMd5 != md5)
                    {
                        await fileChangeRepository.CreateAsync(new FileChangeEntity
                        {
                            ManifestEntryId = existing.Id,
                            RelativePath = relativePath,
                            ChangeType = FileChangeType.Modified,
                            OldMd5Hash = oldMd5,
                            NewMd5Hash = md5,
                            ChangeTimestamp = DateTime.UtcNow,
                            IsProcessed = true
                        });
                    }
                }
                else
                {
                    // Создаём новую запись
                    var newEntry = new ManifestEntryEntity
                    {
                        RelativePath = relativePath,
                        Md5Hash = md5,
                        SizeBytes = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTimeUtc,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await manifestEntryRepository.CreateAsync(newEntry);

                    await fileChangeRepository.CreateAsync(new FileChangeEntity
                    {
                        ManifestEntryId = newEntry.Id,
                        RelativePath = relativePath,
                        ChangeType = FileChangeType.Created,
                        NewMd5Hash = md5,
                        ChangeTimestamp = DateTime.UtcNow,
                        IsProcessed = true
                    });
                }
            }
            else
            {
                // Файл удалён
                if (existing != null)
                {
                    await fileChangeRepository.CreateAsync(new FileChangeEntity
                    {
                        ManifestEntryId = existing.Id,
                        RelativePath = relativePath,
                        ChangeType = FileChangeType.Deleted,
                        OldMd5Hash = existing.Md5Hash,
                        ChangeTimestamp = DateTime.UtcNow,
                        IsProcessed = true
                    });

                    await manifestEntryRepository.DeleteAsync(existing.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update manifest entry: {Path}", relativePath);
        }
    }

    public async Task<ManifestEntryEntity?> GetEntryByIdAsync(string id)
    {
        return await manifestEntryRepository.GetByIdAsync(id);
    }

    public async Task<ManifestEntryEntity?> GetEntryByPathAsync(string relativePath)
    {
        return await manifestEntryRepository.GetByPathAsync(relativePath);
    }

    public async Task<IEnumerable<ManifestEntryEntity>> GetAllEntriesAsync()
    {
        return await manifestEntryRepository.GetAllAsync();
    }

    public async Task<bool> IsManifestUpdatingAsync()
    {
        return _isUpdating;
    }

    public async Task<string> ComputeMd5Async(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            _config.Md5BufferSizeBytes, true);

        using var md5 = MD5.Create();
        var hash = await md5.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    public string GetFilesPath()
    {
        return _config.FilesPath;
    }
}