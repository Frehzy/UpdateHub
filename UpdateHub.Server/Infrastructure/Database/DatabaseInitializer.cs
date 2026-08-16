using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Domain.Enums;
using UpdateHub.Server.Infrastructure.Configuration;
using UpdateHub.Server.Infrastructure.Security;

namespace UpdateHub.Server.Infrastructure.Database;

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
        var directory = Path.GetDirectoryName(Path.GetFullPath(_config.DatabasePath));
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
    /// Пока в сборке нет ни одной миграции, схема создаётся напрямую по модели.
    /// Как только миграции появятся (<c>dotnet ef migrations add Initial</c>),
    /// этот же код автоматически переключится на <c>Migrate</c> — менять ничего не нужно.
    /// </remarks>
    private async Task ApplySchemaAsync(CancellationToken cancellationToken)
    {
        if (context.Database.GetMigrations().Any())
        {
            logger.LogInformation("Применение миграций базы данных");
            await context.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            logger.LogWarning(
                "Миграции не найдены, схема создаётся по модели. " +
                "Выполните 'dotnet ef migrations add Initial', чтобы изменения схемы не требовали удаления базы");
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }
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
