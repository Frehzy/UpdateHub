namespace UpdateHub.BackendServer.Infrastructure.Configuration;

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

    /// <summary>Абсолютный путь к каталогу раздачи.</summary>
    public string ResolvedFilesPath => Resolve(FilesPath);

    /// <summary>Абсолютный путь к файлу базы данных.</summary>
    public string ResolvedDatabasePath => Resolve(DatabasePath);

    /// <summary>Каталог резервных копий, приведённый к абсолютному виду.</summary>
    public string ResolvedBackupPath => Resolve(BackupPath);

    /// <summary>
    /// Каталог резервных копий базы.
    /// </summary>
    /// <remarks>
    /// Обязан лежать вне тома с базой: смысл копии в том, чтобы пережить
    /// потерю тома. В Docker сюда пробрасывается отдельная папка Windows,
    /// которую администратор забирает штатным резервным копированием.
    /// </remarks>
    public string BackupPath { get; set; } = "/app/backup";

    /// <summary>
    /// Как часто снимать копию, в часах. Ноль отключает копирование.
    /// </summary>
    public int BackupIntervalHours { get; set; } = 24;

    /// <summary>
    /// Сколько последних копий хранить.
    /// </summary>
    /// <remarks>
    /// База небольшая — учётные записи, права и история обращений за месяц.
    /// Недельного запаса достаточно, чтобы заметить порчу и откатиться.
    /// </remarks>
    public int BackupKeepCount { get; set; } = 7;

    /// <summary>
    /// Через сколько суток без обращений компьютер считается потерянным.
    /// </summary>
    public int StaleClientDays { get; set; } = 7;

    /// <summary>
    /// Приводит путь из конфигурации к абсолютному виду.
    /// </summary>
    /// <param name="path">Путь: абсолютный либо относительный.</param>
    /// <returns>Абсолютный путь.</returns>
    /// <remarks>
    /// Относительный путь разрешается от каталога с исполняемым файлом, а не от
    /// текущего каталога процесса. Текущий каталог задаёт запускающая сторона:
    /// Visual Studio ставит его в корень проекта, служба Windows — в
    /// <c>C:\Windows\System32</c>, а <c>dotnet</c> из консоли — туда, откуда его
    /// позвали. Из-за этого одна и та же настройка приводила бы к разным папкам
    /// при разных способах запуска. Привязка к каталогу сборки делает поведение
    /// одинаковым: файлы окажутся рядом с приложением, в <c>bin\Debug\net10.0</c>.
    /// В Docker пути заданы абсолютными, поэтому там ничего не меняется.
    /// </remarks>
    public static string Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            path = ".";
        }

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }
}
