using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Clients;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Enrollments;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Groups;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Manifest;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Updates;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Users;
using UpdateHub.BackendServer.Application.Abstractions.Repositories;
using UpdateHub.BackendServer.Application.Abstractions.Services.Clients;
using UpdateHub.BackendServer.Application.Abstractions.Services.Enrollments;
using UpdateHub.BackendServer.Application.Abstractions.Services.Groups;
using UpdateHub.BackendServer.Application.Abstractions.Services.Manifest;
using UpdateHub.BackendServer.Application.Abstractions.Services.Updates;
using UpdateHub.BackendServer.Application.Abstractions.Services.Users;
using UpdateHub.BackendServer.Application.BackgroundServices;
using UpdateHub.BackendServer.Application.Manifest;
using UpdateHub.BackendServer.Application.Repositories.Clients;
using UpdateHub.BackendServer.Application.Repositories.Enrollments;
using UpdateHub.BackendServer.Application.Repositories.Groups;
using UpdateHub.BackendServer.Application.Repositories.Manifest;
using UpdateHub.BackendServer.Application.Repositories.Updates;
using UpdateHub.BackendServer.Application.Repositories.Users;
using UpdateHub.BackendServer.Application.Repositories;
using UpdateHub.BackendServer.Application.Services.Clients;
using UpdateHub.BackendServer.Application.Services.Enrollments;
using UpdateHub.BackendServer.Application.Services.Groups;
using UpdateHub.BackendServer.Application.Services.Manifest;
using UpdateHub.BackendServer.Application.Services.Updates;
using UpdateHub.BackendServer.Application.Services.Users;
using UpdateHub.BackendServer.Infrastructure.Configuration;
using UpdateHub.BackendServer.Infrastructure.Database;
using UpdateHub.BackendServer.Infrastructure.Security;

namespace UpdateHub.BackendServer.Infrastructure.Extensions;

/// <summary>Регистрация служб приложения в контейнере зависимостей.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует настройки приложения.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <param name="configuration">Источник конфигурации.</param>
    /// <returns>Коллекция служб.</returns>
    /// <remarks>
    /// Настройки регистрируются только через <c>Configure</c>. Прежняя версия
    /// клала готовый объект через <c>AddSingleton</c>, а читала его через
    /// <c>IOptions</c> — и получала экземпляр со значениями по умолчанию,
    /// потому что для <c>IOptions</c> ничего настроено не было.
    /// </remarks>
    public static IServiceCollection AddAppConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<UpdateHubConfig>(configuration.GetSection("UpdateHub"));
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<BootstrapAdminSettings>(configuration.GetSection("BootstrapAdmin"));

        return services;
    }

    /// <summary>
    /// Регистрирует контекст базы данных.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <param name="configuration">Источник конфигурации.</param>
    /// <returns>Коллекция служб.</returns>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var databasePath = UpdateHubConfig.Resolve(
            configuration.GetValue<string>("UpdateHub:DatabasePath") ?? new UpdateHubConfig().DatabasePath);

        // Ожидание блокировки вместо мгновенной ошибки: при одновременном
        // обращении нескольких клиентов и работе сканера конкуренция за запись неизбежна.
        var connectionString = $"Data Source={databasePath};Cache=Shared;Default Timeout=30";

        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<DatabaseInitializer>();

        return services;
    }

    /// <summary>
    /// Регистрирует репозитории.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Коллекция служб.</returns>
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientComputerInfoRepository, ClientComputerInfoRepository>();
        services.AddScoped<IClientNetworkInfoRepository, ClientNetworkInfoRepository>();
        services.AddScoped<IClientHistoryRepository, ClientHistoryRepository>();
        services.AddScoped<IClientBlockHistoryRepository, ClientBlockHistoryRepository>();
        services.AddScoped<IUserClientAccessRepository, UserClientAccessRepository>();
        services.AddScoped<IUserGroupAccessRepository, UserGroupAccessRepository>();
        services.AddScoped<IManifestEntryRepository, ManifestEntryRepository>();
        services.AddScoped<IUpdateRequestRepository, UpdateRequestRepository>();
        services.AddScoped<IUpdateDetailRepository, UpdateDetailRepository>();
        services.AddScoped<IFileChangeRepository, FileChangeRepository>();
        services.AddScoped<IEnrollmentRequestRepository, EnrollmentRequestRepository>();

        return services;
    }

    /// <summary>
    /// Регистрирует прикладные службы и фоновые задачи.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <returns>Коллекция служб.</returns>
    /// <remarks>
    /// Состояние манифеста — единственный экземпляр на приложение: в нём живут
    /// семафор обхода и номер поколения. Фоновые задачи берут службы с областью
    /// жизни запроса через <c>IServiceScopeFactory</c>, а не внедряют их напрямую.
    /// </remarks>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<ManifestState>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IClientAccessService, ClientAccessService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<IManifestService, ManifestService>();
        services.AddScoped<IManifestScanService, ManifestScanService>();
        services.AddScoped<ISyncService, SyncService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IEnrollmentService, EnrollmentService>();

        services.AddHostedService<ManifestScanBackgroundService>();
        services.AddHostedService<CleanupBackgroundService>();
        // Служба копий заводится единственным экземпляром и уже он
        // отдаётся фоновой задаче: администратор снимает копию кнопкой,
        // не дожидаясь расписания, и это должна быть та же служба.
        services.AddSingleton<BackupBackgroundService>();
        services.AddHostedService(provider => provider.GetRequiredService<BackupBackgroundService>());

        return services;
    }

    /// <summary>
    /// Настраивает проверку JWT и правила авторизации.
    /// </summary>
    /// <param name="services">Коллекция служб.</param>
    /// <param name="configuration">Источник конфигурации.</param>
    /// <returns>Коллекция служб.</returns>
    /// <exception cref="InvalidOperationException">Ключ подписи не задан или слишком короткий.</exception>
    /// <remarks>
    /// Используется штатный механизм ASP.NET Core вместо самописного разбора
    /// заголовка. Прежняя версия проверяла подпись в своём middleware, но нигде
    /// не сверяла роль, из-за чего любой действующий токен открывал панель управления.
    /// </remarks>
    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

        if (string.IsNullOrWhiteSpace(jwt.SecretKey) || Encoding.UTF8.GetByteCount(jwt.SecretKey) < 32)
        {
            throw new InvalidOperationException(
                "Не задан ключ подписи Jwt:SecretKey либо он короче 32 байт. " +
                "Задайте переменную окружения Jwt__SecretKey, например значением команды " +
                "'openssl rand -base64 48'");
        }

        var workFactor = configuration.GetValue("Security:PasswordWorkFactor", 12);
        services.AddSingleton(new PasswordHasher(workFactor));
        services.AddSingleton<TokenGenerator>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };

                // Ответ на неудачную проверку — текст, а не JSON: клиентская часть
                // API целиком текстовая, и bash-скрипту незачем разбирать JSON.
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async challengeContext =>
                    {
                        challengeContext.HandleResponse();

                        if (challengeContext.Response.HasStarted)
                        {
                            return;
                        }

                        challengeContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        challengeContext.Response.ContentType = "text/plain; charset=utf-8";
                        await challengeContext.Response.WriteAsync("error=Требуется действующий access-токен\n");
                    },
                    OnForbidden = async forbiddenContext =>
                    {
                        if (forbiddenContext.Response.HasStarted)
                        {
                            return;
                        }

                        forbiddenContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                        forbiddenContext.Response.ContentType = "text/plain; charset=utf-8";
                        await forbiddenContext.Response.WriteAsync("error=Недостаточно прав для этой операции\n");
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
