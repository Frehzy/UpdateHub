namespace UpdateHub.Server.Infrastructure.Configuration;

/// <summary>
/// Настройки раздачи файлов и сканирования каталога обновлений.
/// Читаются из секции <c>UpdateHub</c> файла appsettings.json и могут быть
/// переопределены переменными окружения вида <c>UpdateHub__FilesPath</c>.
/// </summary>
public class UpdateHubConfig
{
    /// <summary>
    /// Каталог с раздаваемыми файлами. В Docker сюда монтируется папка Windows,
    /// поэтому доступ к нему заведомо медленный — см. <see cref="PollIntervalSeconds"/>.
    /// </summary>
    public string FilesPath { get; set; } = "/app/files";

    /// <summary>
    /// Путь к файлу базы SQLite. Обязан указывать на именованный том Docker,
    /// а не на проброшенную папку Windows: блокировки файлов через 9p/virtiofs
    /// работают неправильно и приводят к повреждению базы.
    /// </summary>
    public string DatabasePath { get; set; } = "/app/data/updatehub.db";

    /// <summary>
    /// Период опроса каталога <see cref="FilesPath"/> в секундах.
    /// Опрос используется вместо FileSystemWatcher, потому что inotify-события
    /// не проходят через проброс папки Windows в Linux-контейнер.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Сколько секунд файл должен пролежать без изменений, прежде чем попадёт в манифест.
    /// Защищает от вычисления MD5 наполовину скопированного файла.
    /// </summary>
    public int FileSettleSeconds { get; set; } = 15;

    /// <summary>
    /// Размер буфера чтения при вычислении MD5, в байтах.
    /// </summary>
    public int Md5BufferSizeBytes { get; set; } = 65536;

    /// <summary>
    /// Максимальное число строк в манифесте, присланном клиентом.
    /// Ограничивает объём разбираемого тела запроса.
    /// </summary>
    public int MaxClientManifestEntries { get; set; } = 10000;

    /// <summary>
    /// Сколько суток хранить записи о запросах клиентов перед удалением фоновой очисткой.
    /// </summary>
    public int RequestRetentionDays { get; set; } = 30;

    /// <summary>
    /// Сколько суток хранить историю изменений файлов и клиентов.
    /// </summary>
    public int HistoryRetentionDays { get; set; } = 180;
}
