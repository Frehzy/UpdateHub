using Microsoft.Extensions.Options;
using UpdateHub.BackendServer.Application.Abstractions.Repositories;
using UpdateHub.BackendServer.Infrastructure.Configuration;

namespace UpdateHub.BackendServer.Application.BackgroundServices;

/// <summary>
/// Ежесуточно удаляет устаревшие журнальные записи и просроченные токены.
/// </summary>
/// <param name="scopeFactory">Фабрика областей внедрения зависимостей.</param>
/// <param name="config">Настройки хранения.</param>
/// <param name="logger">Журнал.</param>
/// <remarks>
/// Удаление выполняется одним SQL-запросом на таблицу. Прежняя версия выбирала
/// все записи в память и удаляла их по одной, передавая числовой ключ строкой, —
/// очистка падала на первой же записи и уходила в бесконечный цикл повторов.
/// </remarks>
public class CleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<UpdateHubConfig> config,
    ILogger<CleanupBackgroundService> logger) : BackgroundService
{
    private readonly UpdateHubConfig _config = config.Value;

    /// <summary>Час суток по UTC, в который выполняется очистка.</summary>
    private const int CleanupHourUtc = 3;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Служба очистки запущена");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(GetDelayUntilNextRun(), stoppingToken);
                await RunCleanupAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Штатная остановка приложения.
        }

        logger.LogInformation("Служба очистки остановлена");
    }

    /// <summary>
    /// Вычисляет время до ближайшего запуска очистки.
    /// </summary>
    /// <returns>Интервал ожидания.</returns>
    private static TimeSpan GetDelayUntilNextRun()
    {
        var now = DateTime.UtcNow;
        var todayRun = now.Date.AddHours(CleanupHourUtc);
        var nextRun = todayRun > now ? todayRun : todayRun.AddDays(1);
        return nextRun - now;
    }

    /// <summary>
    /// Выполняет одну очистку в отдельной области зависимостей.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var provider = scope.ServiceProvider;

            var requestCutoff = DateTime.UtcNow.AddDays(-_config.RequestRetentionDays);
            var historyCutoff = DateTime.UtcNow.AddDays(-_config.HistoryRetentionDays);

            // Детализация удаляется каскадом вместе с обращениями.
            var requests = await provider.GetRequiredService<IUpdateRequestRepository>()
                .DeleteOlderThanAsync(requestCutoff, cancellationToken);

            var changes = await provider.GetRequiredService<IFileChangeRepository>()
                .DeleteOlderThanAsync(historyCutoff, cancellationToken);

            var history = await provider.GetRequiredService<IClientHistoryRepository>()
                .DeleteOlderThanAsync(historyCutoff, cancellationToken);

            var tokens = await provider.GetRequiredService<IRefreshTokenRepository>()
                .DeleteExpiredAsync(cancellationToken);

            var networks = await provider.GetRequiredService<IClientNetworkInfoRepository>()
                .DeactivateOlderThanAsync(requestCutoff, cancellationToken);

            logger.LogInformation(
                "Очистка завершена: обращений {Requests}, изменений файлов {Changes}, " +
                "истории компьютеров {History}, токенов {Tokens}, деактивировано адресов {Networks}",
                requests, changes, history, tokens, networks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при очистке устаревших записей");
        }
    }
}
