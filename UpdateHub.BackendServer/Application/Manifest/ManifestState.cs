namespace UpdateHub.BackendServer.Application.Manifest;

/// <summary>
/// Общее состояние манифеста, разделяемое сканером и обработчиками запросов.
/// </summary>
/// <remarks>
/// Регистрируется единственным экземпляром на всё приложение. Прежняя версия
/// держала семафор и признак обновления в полях сервиса с областью жизни запроса,
/// поэтому у каждого запроса был свой семафор, взаимное исключение не работало,
/// а признак «идёт пересборка» всегда читался как ложь.
/// </remarks>
public sealed class ManifestState
{
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private long _generation;
    private int _scanning;

    /// <summary>
    /// Номер поколения манифеста. Увеличивается только когда обход
    /// действительно нашёл изменения. Используется для журнала и панели управления.
    /// </summary>
    public long Generation => Interlocked.Read(ref _generation);

    /// <summary>Признак того, что обход каталога выполняется прямо сейчас.</summary>
    public bool IsScanning => Volatile.Read(ref _scanning) != 0;

    /// <summary>Момент завершения последнего успешного обхода.</summary>
    public DateTime? LastScanCompletedAt { get; private set; }

    /// <summary>Число файлов в манифесте по итогам последнего обхода.</summary>
    public int EntryCount { get; private set; }

    /// <summary>Суммарный объём файлов манифеста в байтах.</summary>
    public long TotalSizeBytes { get; private set; }

    /// <summary>
    /// Пути, отвергнутые при последнем обходе, с причиной отказа.
    /// Показываются администратору, чтобы неудачное имя файла не пропало молча.
    /// </summary>
    public IReadOnlyList<string> RejectedPaths { get; private set; } = [];

    /// <summary>
    /// Пытается занять исключительное право на обход каталога.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>
    /// Объект, освобождающий право при уничтожении, либо <see langword="null"/>,
    /// если обход уже выполняется другим вызовом.
    /// </returns>
    public async Task<IDisposable?> TryBeginScanAsync(CancellationToken cancellationToken = default)
    {
        if (!await _scanLock.WaitAsync(0, cancellationToken))
        {
            return null;
        }

        Interlocked.Exchange(ref _scanning, 1);
        return new ScanScope(this);
    }

    /// <summary>
    /// Фиксирует итоги успешного обхода.
    /// </summary>
    /// <param name="entryCount">Число файлов в манифесте.</param>
    /// <param name="totalSizeBytes">Суммарный объём файлов.</param>
    /// <param name="rejectedPaths">Отвергнутые пути с причинами.</param>
    /// <param name="hasChanges">Были ли обнаружены изменения.</param>
    public void CompleteScan(int entryCount, long totalSizeBytes, IReadOnlyList<string> rejectedPaths, bool hasChanges)
    {
        EntryCount = entryCount;
        TotalSizeBytes = totalSizeBytes;
        RejectedPaths = rejectedPaths;
        LastScanCompletedAt = DateTime.UtcNow;

        if (hasChanges)
        {
            Interlocked.Increment(ref _generation);
        }
    }

    /// <summary>Освобождает право на обход при уничтожении.</summary>
    /// <param name="state">Владелец состояния.</param>
    private sealed class ScanScope(ManifestState state) : IDisposable
    {
        /// <summary>Снимает признак обхода и освобождает семафор.</summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref state._scanning, 0);
            state._scanLock.Release();
        }
    }
}
