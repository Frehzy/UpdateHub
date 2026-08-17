using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using UpdateHub.BackendServer.Application.Abstractions.Repositories;
using UpdateHub.BackendServer.Application.Abstractions.Services;
using UpdateHub.BackendServer.Application.Manifest;
using UpdateHub.BackendServer.Domain.Entities;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.BackendServer.Domain.ValueObjects;
using UpdateHub.BackendServer.Infrastructure.Configuration;

namespace UpdateHub.BackendServer.Application.Services;

/// <summary>
/// Обходит каталог раздачи и приводит эталонный манифест в соответствие с диском.
/// </summary>
/// <param name="config">Настройки раздачи.</param>
/// <param name="state">Общее состояние манифеста.</param>
/// <param name="manifestEntryRepository">Доступ к записям манифеста.</param>
/// <param name="fileChangeRepository">Доступ к истории изменений файлов.</param>
/// <param name="logger">Журнал.</param>
public class ManifestScanService(
    IOptions<UpdateHubConfig> config,
    ManifestState state,
    IManifestEntryRepository manifestEntryRepository,
    IFileChangeRepository fileChangeRepository,
    ILogger<ManifestScanService> logger) : IManifestScanService
{
    private readonly UpdateHubConfig _config = config.Value;

    /// <inheritdoc />
    public async Task<ManifestScanResult> ScanAsync(CancellationToken cancellationToken = default)
    {
        using var scope = await state.TryBeginScanAsync(cancellationToken);
        if (scope is null)
        {
            logger.LogDebug("Обход каталога уже выполняется, повторный запуск пропущен");
            return ManifestScanResult.Skipped;
        }

        var filesPath = _config.ResolvedFilesPath;
        if (!Directory.Exists(filesPath))
        {
            Directory.CreateDirectory(filesPath);
            logger.LogWarning("Каталог раздачи отсутствовал и был создан: {FilesPath}", filesPath);
        }

        var existing = await manifestEntryRepository.GetAllByPathAsync(cancellationToken);
        var rejected = new List<string>();
        var changes = new List<FileChangeEntity>();
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);

        // Пути, обработку которых пришлось отложить: файл ещё дописывается либо
        // его не удалось прочитать. Их нельзя считать исчезнувшими, иначе запись
        // выпадет из манифеста на время копирования и клиенты не смогут скачать файл.
        var deferredPaths = new HashSet<string>(StringComparer.Ordinal);

        var candidates = CollectCandidates(filesPath, rejected);

        var settleThreshold = DateTime.UtcNow.AddSeconds(-_config.FileSettleSeconds);
        var hashedCount = 0;
        var totalSize = 0L;

        foreach (var (path, fullPath) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(fullPath);
                if (!fileInfo.Exists)
                {
                    continue;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                rejected.Add($"{path}: {ex.Message}");
                deferredPaths.Add(path);
                continue;
            }

            // Файл, изменённый только что, может ещё дописываться. Считать с него
            // MD5 — значит записать в манифест сумму половины файла и заставить
            // клиентов бесконечно перекачивать то, что никогда не сойдётся.
            if (fileInfo.LastWriteTimeUtc > settleThreshold)
            {
                logger.LogDebug("Файл {Path} изменён только что, обработка отложена", path);
                deferredPaths.Add(path);
                continue;
            }

            seenPaths.Add(path);
            totalSize += fileInfo.Length;

            existing.TryGetValue(path, out var entry);

            // Ключевая оптимизация: MD5 пересчитывается только когда размер или
            // время изменения разошлись с сохранёнными. Иначе каждый обход читал бы
            // весь каталог целиком через медленный проброс папки Windows.
            if (entry is not null &&
                entry.SizeBytes == fileInfo.Length &&
                entry.LastModified == fileInfo.LastWriteTimeUtc)
            {
                continue;
            }

            string md5;
            try
            {
                md5 = await ComputeMd5Async(fullPath, cancellationToken);
                hashedCount++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                rejected.Add($"{path}: не удалось прочитать файл — {ex.Message}");
                seenPaths.Remove(path);
                deferredPaths.Add(path);
                continue;
            }

            if (entry is null)
            {
                entry = new ManifestEntryEntity
                {
                    RelativePath = path,
                    Md5Hash = md5,
                    SizeBytes = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTimeUtc
                };

                await manifestEntryRepository.CreateAsync(entry, cancellationToken);
                changes.Add(new FileChangeEntity
                {
                    ManifestEntryId = entry.Id,
                    RelativePath = path,
                    ChangeType = FileChangeType.Created,
                    NewMd5Hash = md5
                });

                logger.LogInformation("В манифест добавлен файл {Path} ({Size} байт)", path, fileInfo.Length);
            }
            else
            {
                var oldMd5 = entry.Md5Hash;
                entry.Md5Hash = md5;
                entry.SizeBytes = fileInfo.Length;
                entry.LastModified = fileInfo.LastWriteTimeUtc;
                entry.UpdatedAt = DateTime.UtcNow;
                await manifestEntryRepository.UpdateAsync(entry, cancellationToken);

                if (!string.Equals(oldMd5, md5, StringComparison.Ordinal))
                {
                    changes.Add(new FileChangeEntity
                    {
                        ManifestEntryId = entry.Id,
                        RelativePath = path,
                        ChangeType = FileChangeType.Modified,
                        OldMd5Hash = oldMd5,
                        NewMd5Hash = md5
                    });

                    logger.LogInformation("Файл {Path} изменился", path);
                }
            }
        }

        var removedPaths = existing.Keys
            .Where(p => !seenPaths.Contains(p) && !deferredPaths.Contains(p))
            .ToList();
        foreach (var path in removedPaths)
        {
            changes.Add(new FileChangeEntity
            {
                ManifestEntryId = null,
                RelativePath = path,
                ChangeType = FileChangeType.Deleted,
                OldMd5Hash = existing[path].Md5Hash
            });
        }

        if (removedPaths.Count > 0)
        {
            await manifestEntryRepository.DeleteByPathsAsync(removedPaths, cancellationToken);
            logger.LogInformation("Из манифеста удалено файлов: {Count}", removedPaths.Count);
        }

        await fileChangeRepository.AddRangeAsync(changes, cancellationToken);

        var hasChanges = changes.Count > 0;
        state.CompleteScan(seenPaths.Count, totalSize, rejected, hasChanges);

        if (rejected.Count > 0)
        {
            logger.LogWarning("Файлов отвергнуто при обходе: {Count}. Первые: {Paths}",
                rejected.Count, string.Join("; ", rejected.Take(5)));
        }

        // Обход выполняется каждые несколько десятков секунд и в обычной жизни
        // ничего не находит. Писать об этом на уровне Information — значит утопить
        // в однообразных строках всё, что действительно стоит прочитать.
        if (hasChanges)
        {
            logger.LogInformation(
                "Манифест обновлён: файлов {Count}, пересчитано MD5 {Hashed}, изменений {Changes}, поколение {Generation}",
                seenPaths.Count, hashedCount, changes.Count, state.Generation);
        }
        else
        {
            logger.LogDebug(
                "Обход завершён без изменений: файлов {Count}, поколение {Generation}",
                seenPaths.Count, state.Generation);
        }

        return new ManifestScanResult(true, seenPaths.Count, hashedCount, changes.Count, rejected);
    }

    /// <inheritdoc />
    public async Task<string> ComputeMd5Async(string fullPath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            _config.Md5BufferSizeBytes,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var md5 = MD5.Create();
        var hash = await md5.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Составляет список файлов, пригодных для попадания в манифест.
    /// </summary>
    /// <param name="root">Корень обхода.</param>
    /// <param name="rejected">Приёмник сообщений об отвергнутых путях.</param>
    /// <returns>Пары «относительный путь — полный путь».</returns>
    /// <remarks>
    /// Отсеивает недопустимые имена и конфликты регистра. Конфликт возможен потому,
    /// что каталог лежит на NTFS, которая не различает регистр, а клиент работает
    /// на ext4, которая различает: <c>Doc.txt</c> и <c>doc.txt</c> на сервере
    /// неразличимы, а на клиенте это два разных файла. Отбрасываются обе стороны —
    /// иначе выбор зависел бы от порядка обхода каталога.
    /// </remarks>
    private static List<(string Path, string FullPath)> CollectCandidates(string root, List<string> rejected)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false,
            AttributesToSkip = FileAttributes.System
        };

        var byCaseInsensitiveKey = new Dictionary<string, List<(string Path, string FullPath)>>(StringComparer.OrdinalIgnoreCase);

        foreach (var fullPath in Directory.EnumerateFiles(root, "*", options))
        {
            var rawRelative = Path.GetRelativePath(root, fullPath).Replace('\\', '/');

            if (!RelativePath.TryCreate(rawRelative, out var relativePath, out var pathError))
            {
                rejected.Add($"{rawRelative}: {pathError}");
                continue;
            }

            var path = relativePath!.Value;

            if (!byCaseInsensitiveKey.TryGetValue(path, out var bucket))
            {
                bucket = [];
                byCaseInsensitiveKey[path] = bucket;
            }

            bucket.Add((path, fullPath));
        }

        var result = new List<(string Path, string FullPath)>(byCaseInsensitiveKey.Count);

        foreach (var bucket in byCaseInsensitiveKey.Values)
        {
            if (bucket.Count == 1)
            {
                result.Add(bucket[0]);
                continue;
            }

            var names = string.Join(", ", bucket.Select(b => b.Path));
            foreach (var item in bucket)
            {
                rejected.Add($"{item.Path}: конфликт регистра ({names})");
            }
        }

        return result;
    }
}
