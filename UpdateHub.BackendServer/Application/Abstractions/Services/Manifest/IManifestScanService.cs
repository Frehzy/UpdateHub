namespace UpdateHub.BackendServer.Application.Abstractions.Services.Manifest;

/// <summary>
/// Приведение эталонного манифеста в соответствие с содержимым каталога раздачи.
/// </summary>
public interface IManifestScanService
{
    /// <summary>
    /// Выполняет обход каталога и обновляет манифест.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итоги обхода; если обход уже шёл, возвращается результат с отметкой пропуска.</returns>
    Task<ManifestScanResult> ScanAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Вычисляет контрольную сумму файла.
    /// </summary>
    /// <param name="fullPath">Полный путь к файлу.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>MD5 в нижнем регистре.</returns>
    Task<string> ComputeMd5Async(string fullPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Итоги обхода каталога раздачи.
/// </summary>
/// <param name="Executed">Обход был выполнен, а не пропущен из-за параллельного запуска.</param>
/// <param name="TotalFiles">Число файлов в манифесте после обхода.</param>
/// <param name="HashedFiles">Сколько файлов потребовали пересчёта MD5.</param>
/// <param name="Changes">Число зафиксированных изменений.</param>
/// <param name="RejectedPaths">Отвергнутые пути с указанием причины.</param>
public sealed record ManifestScanResult(
    bool Executed,
    int TotalFiles,
    int HashedFiles,
    int Changes,
    IReadOnlyList<string> RejectedPaths)
{
    /// <summary>Результат для случая, когда обход уже выполнялся и запуск пропущен.</summary>
    public static ManifestScanResult Skipped { get; } = new(false, 0, 0, 0, []);
}
