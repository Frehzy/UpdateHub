using Microsoft.Extensions.Options;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Clients;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Manifest;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Updates;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Users;
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
        logger.LogInformation(
            "Служба очистки запущена: обращения хранятся {Requests}, история {History}",
            Describe(_config.RequestRetentionDays),
            Describe(_config.HistoryRetentionDays));

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
    /// Описывает срок хранения для журнала.
    /// </summary>
    /// <param name="days">Срок в сутках; ноль и меньше означают «не удалять».</param>
    /// <returns>Строка для вывода при старте.</returns>
    /// <remarks>
    /// Отключённая очистка обязана быть видна при запуске. Иначе администратор,
    /// поставивший ноль, узнаёт о последствиях только по накопившейся базе —
    /// или, до появления защиты, по исчезнувшей истории.
    /// </remarks>
    private static string Describe(int days)
        => days > 0 ? $"{days} сут" : "бессрочно (очистка отключена)";

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
    /// <remarks>
    /// Открытый метод, а не закрытый: иначе удаление данных остаётся вне
    /// проверок — вызвать его можно было бы только ожиданием до трёх часов
    /// ночи. Так же сделано у службы резервного копирования.
    /// </remarks>
    public async Task RunCleanupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var provider = scope.ServiceProvider;

            // Ноль и отрицательное значение означают «не удалять», а не «удалить
            // всё». Прежде защиты не было: при RequestRetentionDays = 0 граница
            // приходилась на текущий момент, и ближайшая ночная очистка молча
            // сносила всю историю обращений — без возможности вернуть, на сервере,
            // к которому никто не ходит.
            //
            // Ноль как «отключено» — то же соглашение, что у BackupIntervalHours
            // и BackupKeepCount. Опечатка в настройке не должна уничтожать данные.
            var requests = 0;
            var networks = 0;

            if (_config.RequestRetentionDays > 0)
            {
                var requestCutoff = DateTime.UtcNow.AddDays(-_config.RequestRetentionDays);

                // Детализация удаляется каскадом вместе с обращениями.
                requests = await provider.GetRequiredService<IUpdateRequestRepository>()
                    .DeleteOlderThanAsync(requestCutoff, cancellationToken);

                networks = await provider.GetRequiredService<IClientNetworkInfoRepository>()
                    .DeactivateOlderThanAsync(requestCutoff, cancellationToken);
            }

            var changes = 0;
            var history = 0;

            if (_config.HistoryRetentionDays > 0)
            {
                var historyCutoff = DateTime.UtcNow.AddDays(-_config.HistoryRetentionDays);

                changes = await provider.GetRequiredService<IFileChangeRepository>()
                    .DeleteOlderThanAsync(historyCutoff, cancellationToken);

                history = await provider.GetRequiredService<IClientHistoryRepository>()
                    .DeleteOlderThanAsync(historyCutoff, cancellationToken);
            }

            // Просроченные токены удаляются всегда: срок их жизни задан при
            // выдаче, и хранить недействительные незачем.
            var tokens = await provider.GetRequiredService<IRefreshTokenRepository>()
                .DeleteExpiredAsync(cancellationToken);

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
