using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Infrastructure.Configuration;

namespace UpdateHub.Server.Application.Services;

public class FileWatcherService(
    IOptions<UpdateHubConfig> config,
    IManifestService manifestService,
    ILogger<FileWatcherService> logger) : BackgroundService, IFileWatcherService
{
    private readonly UpdateHubConfig _config = config.Value;
    private readonly ConcurrentQueue<string> _changeQueue = new();
    private FileSystemWatcher? _watcher;
    private Timer? _processTimer;
    private bool _isProcessing;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("FileWatcherService starting");

        // Первоначальная загрузка манифеста
        await manifestService.RefreshManifestAsync(stoppingToken);

        // Настраиваем FileSystemWatcher
        var filesPath = _config.FilesPath;
        if (!Directory.Exists(filesPath))
        {
            Directory.CreateDirectory(filesPath);
        }

        _watcher = new FileSystemWatcher(filesPath)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        _watcher.Created += OnFileChanged;
        _watcher.Changed += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += OnFileRenamed;
        _watcher.Error += OnWatcherError;

        // Таймер для обработки очереди
        _processTimer = new Timer(
            ProcessQueue,
            null,
            TimeSpan.FromSeconds(_config.ManifestRefreshIntervalSeconds),
            TimeSpan.FromSeconds(_config.ManifestRefreshIntervalSeconds));

        logger.LogInformation("FileWatcherService started, watching: {FilesPath}", filesPath);

        // Ожидаем завершения
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("FileWatcherService stopping");

        _processTimer?.Dispose();
        _watcher?.Dispose();

        await base.StopAsync(cancellationToken);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var relativePath = Path.GetRelativePath(_config.FilesPath, e.FullPath).Replace('\\', '/');
        _changeQueue.Enqueue(relativePath);
        logger.LogDebug("File change queued: {Path} ({ChangeType})", relativePath, e.ChangeType);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Обрабатываем как удаление старого и создание нового
        var oldPath = Path.GetRelativePath(_config.FilesPath, e.OldFullPath).Replace('\\', '/');
        var newPath = Path.GetRelativePath(_config.FilesPath, e.FullPath).Replace('\\', '/');

        _changeQueue.Enqueue(oldPath);
        _changeQueue.Enqueue(newPath);
        logger.LogDebug("File rename queued: {OldPath} -> {NewPath}", oldPath, newPath);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        logger.LogError(e.GetException(), "FileSystemWatcher error");
    }

    private async void ProcessQueue(object? state)
    {
        if (_isProcessing || _changeQueue.IsEmpty)
        {
            return;
        }

        try
        {
            _isProcessing = true;
            var processed = new HashSet<string>();

            while (_changeQueue.TryDequeue(out var path))
            {
                if (!processed.Contains(path))
                {
                    processed.Add(path);
                    await manifestService.UpdateManifestEntryAsync(path);
                }
            }

            if (processed.Count != 0)
            {
                logger.LogDebug("Processed {Count} file changes", processed.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing file changes");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    public new Task StartAsync(CancellationToken cancellationToken)
    {
        return ExecuteAsync(cancellationToken);
    }
}