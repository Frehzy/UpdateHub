using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UpdateHub.BackendServer.Infrastructure.Configuration;
using UpdateHub.BackendServer.Infrastructure.Database;

namespace UpdateHub.BackendServer.Application.BackgroundServices;

/// <summary>
/// Делает резервные копии базы данных по расписанию.
/// </summary>
/// <remarks>
/// База живёт на именованном томе Docker и существует в единственном
/// экземпляре. Вместе с ней исчезли бы учётные записи, все выданные права
/// и вся история обращений — восстановить это неоткуда, а сервер стоит
/// в контуре без интернета, куда нужно ехать.
/// <para>
/// Копия снимается командой <c>VACUUM INTO</c>, а не копированием файла.
/// Копировать файл базы на ходу нельзя: в режиме WAL часть записей лежит
/// в отдельном журнале, и копия окажется без них — то есть повреждённой.
/// <c>VACUUM INTO</c> собирает целостный снимок средствами самой SQLite.
/// </para>
/// <para>
/// Каталог копий обязан лежать вне тома с базой: смысл в том, чтобы копия
/// пережила потерю тома. В Docker под него отводится отдельная проброшенная
/// папка — та, которую администратор забирает штатным резервным копированием.
/// </para>
/// </remarks>
/// <param name="scopeFactory">Фабрика областей: контекст базы живёт в области запроса.</param>
/// <param name="config">Настройки.</param>
/// <param name="logger">Журнал.</param>
public class BackupBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<UpdateHubConfig> config,
    ILogger<BackupBackgroundService> logger) : BackgroundService
{
    private readonly UpdateHubConfig _config = config.Value;

    /// <summary>Образец имени файла копии.</summary>
    private const string FileNameFormat = "updatehub-{0:yyyyMMdd-HHmmss}.db";

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_config.BackupIntervalHours <= 0)
        {
            logger.LogInformation("Резервное копирование отключено настройкой BackupIntervalHours");
            return;
        }

        logger.LogInformation(
            "Резервное копирование включено: каждые {Hours} ч в {Path}, хранить копий {Count}",
            _config.BackupIntervalHours, _config.ResolvedBackupPath, _config.BackupKeepCount);

        try
        {
            // Первая копия снимается сразу после старта, а не через сутки:
            // иначе перезапускаемый раз в день сервер не сделал бы её никогда.
            while (!stoppingToken.IsCancellationRequested)
            {
                await CreateBackupAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromHours(_config.BackupIntervalHours), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Штатная остановка приложения.
        }

        logger.LogInformation("Резервное копирование остановлено");
    }

    /// <summary>
    /// Снимает одну копию и убирает устаревшие.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Путь к созданной копии или <c>null</c>, если снять не удалось.</returns>
    public async Task<string?> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = _config.ResolvedBackupPath;
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, string.Format(FileNameFormat, DateTime.UtcNow));

            // VACUUM INTO отказывается писать в существующий файл. Совпадение
            // возможно только при двух копиях в одну секунду — например, если
            // администратор нажал кнопку в момент срабатывания расписания.
            if (File.Exists(path))
            {
                logger.LogDebug("Копия {Path} уже существует, снимок пропущен", path);
                return null;
            }

            using var scope = scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Путь подставляется в текст запроса, потому что VACUUM INTO
            // не принимает параметров. Он приходит из файла настроек, не от
            // пользователя, но одинарные кавычки всё равно экранируются.
            var quoted = path.Replace("'", "''");

            // EF1002 предупреждает о подстановке в текст запроса. Здесь она
            // неизбежна — параметр SQLite в этой команде не примет, — а значение
            // экранировано строкой выше, поэтому предупреждение снимается точечно.
#pragma warning disable EF1002
            await context.Database.ExecuteSqlRawAsync($"VACUUM INTO '{quoted}'", cancellationToken);
#pragma warning restore EF1002

            var size = new FileInfo(path).Length;
            logger.LogInformation("Снята резервная копия базы: {Path} ({Size} байт)", path, size);

            RemoveOutdated(directory);
            return path;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Неудачная копия не должна ронять сервер: раздача файлов важнее.
            logger.LogError(ex, "Не удалось снять резервную копию базы");
            return null;
        }
    }

    /// <summary>
    /// Удаляет копии сверх заданного количества, начиная со старых.
    /// </summary>
    /// <param name="directory">Каталог копий.</param>
    /// <remarks>
    /// Считаются только файлы, созданные этой службой: в том же каталоге может
    /// лежать что-то ещё, и удалять чужое она не вправе.
    /// </remarks>
    private void RemoveOutdated(string directory)
    {
        if (_config.BackupKeepCount <= 0)
        {
            return;
        }

        var outdated = new DirectoryInfo(directory)
            .GetFiles("updatehub-*.db")
            .OrderByDescending(file => file.Name, StringComparer.Ordinal)
            .Skip(_config.BackupKeepCount)
            .ToList();

        foreach (var file in outdated)
        {
            try
            {
                file.Delete();
                logger.LogDebug("Удалена устаревшая копия: {Name}", file.Name);
            }
            catch (IOException ex)
            {
                logger.LogWarning(ex, "Не удалось удалить устаревшую копию {Name}", file.Name);
            }
        }
    }
}
