using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Users;
using UpdateHub.BackendServer.Application.Abstractions.Repositories;
using UpdateHub.BackendServer.Domain.Entities.Users;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.BackendServer.Infrastructure.Configuration;
using UpdateHub.BackendServer.Infrastructure.Security;
using UpdateHub.Shared.Enums;

namespace UpdateHub.BackendServer.Infrastructure.Database;

/// <summary>
/// Готовит базу данных к работе при старте приложения: создаёт каталог,
/// применяет схему, включает режим WAL и заводит первого администратора.
/// </summary>
/// <param name="context">Контекст базы данных.</param>
/// <param name="userRepository">Доступ к учётным записям.</param>
/// <param name="passwordHasher">Хэширование паролей.</param>
/// <param name="config">Настройки раздачи.</param>
/// <param name="bootstrapAdmin">Учётные данные первого администратора.</param>
/// <param name="logger">Журнал.</param>
public class DatabaseInitializer(
    AppDbContext context,
    IUserRepository userRepository,
    PasswordHasher passwordHasher,
    IOptions<UpdateHubConfig> config,
    IOptions<BootstrapAdminSettings> bootstrapAdmin,
    ILogger<DatabaseInitializer> logger)
{
    private readonly UpdateHubConfig _config = config.Value;
    private readonly BootstrapAdminSettings _bootstrapAdmin = bootstrapAdmin.Value;

    /// <summary>
    /// Выполняет полную подготовку базы.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureDatabaseDirectoryExists();
        await ApplySchemaAsync(cancellationToken);
        await ApplySqlitePragmasAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
    }

    /// <summary>
    /// Создаёт каталог для файла базы. SQLite сам каталоги не создаёт
    /// и падает с невнятным «unable to open database file».
    /// </summary>
    private void EnsureDatabaseDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_config.ResolvedDatabasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            logger.LogInformation("Создан каталог для базы данных: {Directory}", directory);
        }
    }

    /// <summary>
    /// Приводит схему в актуальное состояние.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <remarks>
    /// Если в сборке нет ни одной миграции, схема создаётся напрямую по модели —
    /// это запасной путь, он оставлен на случай, когда миграции по какой-то
    /// причине не попали в сборку.
    /// </remarks>
    private async Task ApplySchemaAsync(CancellationToken cancellationToken)
    {
        var migrations = context.Database.GetMigrations().ToList();
        if (migrations.Count == 0)
        {
            logger.LogWarning(
                "Миграции не найдены, схема создаётся по модели. " +
                "Выполните 'dotnet ef migrations add Initial', чтобы изменения схемы не требовали удаления базы");
            await context.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }

        await AdoptSchemaCreatedWithoutMigrationsAsync(migrations[0], cancellationToken);

        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("Схема базы данных актуальна");
            return;
        }

        logger.LogInformation("Применение миграций: {Migrations}", string.Join(", ", pending));
        await context.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Помечает первую миграцию как применённую, если база была создана
    /// прежней версией сервера — вызовом <c>EnsureCreated</c>.
    /// </summary>
    /// <param name="firstMigrationId">Идентификатор первой миграции.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <remarks>
    /// <c>EnsureCreated</c> строит схему, но не ведёт журнал применённых
    /// миграций. Для <c>Migrate</c> такая база выглядит пустой: он попытается
    /// создать таблицы заново и упадёт на «table Users already exists», а сервер
    /// не запустится. Разбираться с этим на площадке без интернета, где базу
    /// нельзя просто удалить вместе с учётными записями и историей, — плохой
    /// сценарий, поэтому переход выполняется здесь и один раз.
    /// <para>
    /// Схема, построенная <c>EnsureCreated</c> по той же модели, совпадает с
    /// результатом первой миграции — тест <c>MigrationsTests</c> сравнивает их
    /// таблица за таблицей, — поэтому запись в журнал безопасна: применять
    /// первую миграцию поверх существующих таблиц не требуется.
    /// </para>
    /// </remarks>
    private async Task AdoptSchemaCreatedWithoutMigrationsAsync(
        string firstMigrationId,
        CancellationToken cancellationToken)
    {
        var history = context.GetService<IHistoryRepository>();
        if (await history.ExistsAsync(cancellationToken))
        {
            return;
        }

        var creator = context.GetService<IRelationalDatabaseCreator>();
        if (!await creator.HasTablesAsync(cancellationToken))
        {
            return;
        }

        logger.LogWarning(
            "База создана без миграций. Миграция {MigrationId} отмечается как применённая, " +
            "существующие данные сохраняются",
            firstMigrationId);

        var version = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "unknown";

        await context.Database.ExecuteSqlRawAsync(history.GetCreateScript(), cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            history.GetInsertScript(new HistoryRow(firstMigrationId, version)),
            cancellationToken);
    }

    /// <summary>
    /// Включает журналирование WAL и таймаут ожидания блокировки.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <remarks>
    /// В режиме по умолчанию писатель блокирует читателей целиком, а конфликтующий
    /// запрос падает мгновенно. WAL разводит их, а <c>busy_timeout</c> заставляет
    /// подождать вместо ошибки «database is locked».
    /// </remarks>
    private async Task ApplySqlitePragmasAsync(CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("PRAGMA synchronous=NORMAL;", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("PRAGMA busy_timeout=30000;", cancellationToken);
        await context.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;", cancellationToken);
        logger.LogInformation("SQLite переведён в режим WAL");
    }

    /// <summary>
    /// Создаёт администратора, если в базе нет ни одной учётной записи.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <remarks>
    /// Без этого систему невозможно ввести в эксплуатацию: завести пользователя
    /// может только администратор, а на пустой базе администратора нет.
    /// </remarks>
    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        if (!await userRepository.IsEmptyAsync(cancellationToken))
        {
            return;
        }

        var generated = string.IsNullOrWhiteSpace(_bootstrapAdmin.Password);
        var password = generated ? GeneratePassword() : _bootstrapAdmin.Password!;

        var admin = new UserEntity
        {
            Username = _bootstrapAdmin.Username,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = UserRole.Admin,
            IsActive = true,
            MustChangePassword = true
        };

        await userRepository.CreateAsync(admin, cancellationToken);

        if (generated)
        {
            logger.LogWarning(
                "Создан администратор '{Username}' со сгенерированным паролем: {Password}. " +
                "Запишите его — повторно узнать пароль нельзя. Смена пароля обязательна при первом входе",
                admin.Username,
                password);
        }
        else
        {
            logger.LogInformation(
                "Создан администратор '{Username}' с паролем из конфигурации. Смена пароля обязательна при первом входе",
                admin.Username);
        }
    }

    /// <summary>
    /// Генерирует случайный пароль, пригодный для однократного ввода вручную.
    /// </summary>
    /// <returns>Пароль из 20 символов без визуально схожих знаков.</returns>
    private static string GeneratePassword()
    {
        const string alphabet = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return RandomNumberGenerator.GetString(alphabet, 20);
    }
}
