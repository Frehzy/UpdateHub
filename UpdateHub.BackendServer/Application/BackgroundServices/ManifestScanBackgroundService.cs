using Microsoft.Extensions.Options;
using UpdateHub.BackendServer.Application.Abstractions.Services;
using UpdateHub.BackendServer.Infrastructure.Configuration;

namespace UpdateHub.BackendServer.Application.BackgroundServices;

/// <summary>
/// Периодически обходит каталог раздачи и обновляет эталонный манифест.
/// </summary>
/// <param name="scopeFactory">Фабрика областей внедрения зависимостей.</param>
/// <param name="config">Настройки раздачи.</param>
/// <param name="logger">Журнал.</param>
/// <remarks>
/// <para>
/// Используется опрос, а не <c>FileSystemWatcher</c>. Каталог раздачи —
/// проброшенная в контейнер папка Windows, а события inotify через 9p/virtiofs
/// не проходят: наблюдатель молча не сработал бы ни разу, и манифест остался бы
/// таким, каким его построили при запуске.
/// </para>
/// <para>
/// Фоновая служба живёт всё время работы приложения, поэтому зависимости с
/// областью жизни запроса берутся из отдельной области на каждой итерации.
/// Внедрять их напрямую нельзя: единственный контекст базы данных,
/// захваченный на весь срок работы, не потокобезопасен и бесконечно копит
/// отслеживаемые сущности.
/// </para>
/// </remarks>
public class ManifestScanBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<UpdateHubConfig> config,
    ILogger<ManifestScanBackgroundService> logger) : BackgroundService
{
    private readonly UpdateHubConfig _config = config.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, _config.PollIntervalSeconds));

        // Путь печатается абсолютным: относительный './files' из конфигурации
        // разрешается от рабочего каталога процесса, и понять по нему, какая
        // папка на самом деле раздаётся, невозможно.
        logger.LogInformation(
            "Сканер каталога запущен: {FilesPath}, опрос каждые {Interval} с",
            _config.ResolvedFilesPath,
            interval.TotalSeconds);

        // Первый обход выполняем сразу, чтобы манифест был готов к приходу клиентов.
        await RunScanAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunScanAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Штатная остановка приложения.
        }

        logger.LogInformation("Сканер каталога остановлен");
    }

    /// <summary>
    /// Выполняет один обход в отдельной области зависимостей.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    private async Task RunScanAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var scanner = scope.ServiceProvider.GetRequiredService<IManifestScanService>();
            await scanner.ScanAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Сбой одного обхода не должен останавливать службу: каталог может быть
            // временно недоступен, если проброшенная папка отвалилась.
            logger.LogError(ex, "Ошибка при обходе каталога раздачи");
        }
    }
}
