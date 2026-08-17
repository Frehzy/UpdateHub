namespace UpdateHub.BackendServer.Infrastructure.Diagnostics;

/// <summary>
/// Свободное место на разделе, где лежит заданный каталог.
/// </summary>
/// <remarks>
/// Нужно администратору: файлы раздачи, база, её журнал WAL и резервные копии
/// живут на дисках сервера, и когда место кончается, копирование начинает
/// отказывать каждые сутки — молча, только в журнале.
/// </remarks>
public static class DiskSpace
{
    /// <summary>
    /// Определяет свободное и общее место для каталога.
    /// </summary>
    /// <param name="path">Каталог; существовать не обязан.</param>
    /// <returns>
    /// Пара «свободно, всего» в байтах либо <c>(null, null)</c>, если раздел
    /// определить не удалось.
    /// </returns>
    /// <remarks>
    /// Раздел ищется по самому длинному корню, с которого начинается путь,
    /// а не по <c>Path.GetPathRoot</c>. Разница важна в контейнере: каталоги
    /// раздачи и копий — отдельные проброшенные папки, и корень «/» показал бы
    /// место внутри образа, а не на диске, куда они на самом деле смотрят.
    /// </remarks>
    public static (long? FreeBytes, long? TotalBytes) Measure(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);

            var drive = DriveInfo.GetDrives()
                .Where(item => item.IsReady && full.StartsWith(item.RootDirectory.FullName, PathComparison))
                .OrderByDescending(item => item.RootDirectory.FullName.Length)
                .FirstOrDefault();

            return drive is null
                ? (null, null)
                : (drive.AvailableFreeSpace, drive.TotalSize);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Раздел недоступен — сведения о месте не настолько важны,
            // чтобы из-за них отказывать в ответе целиком.
            return (null, null);
        }
    }

    /// <summary>
    /// Способ сравнения путей: в Windows регистр не важен, в Linux важен.
    /// </summary>
    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
