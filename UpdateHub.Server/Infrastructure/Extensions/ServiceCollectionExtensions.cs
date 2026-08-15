using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Application.Repositories;
using UpdateHub.Server.Application.Services;
using UpdateHub.Server.Infrastructure.Configuration;
using UpdateHub.Server.Infrastructure.Database;
using UpdateHub.Server.Infrastructure.Security;

namespace UpdateHub.Server.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var config = configuration.GetSection("UpdateHub").Get<UpdateHubConfig>()
            ?? new UpdateHubConfig();

        services.AddSingleton(config);

        var connectionString = $"Data Source={config.DatabasePath}";
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientComputerInfoRepository, ClientComputerInfoRepository>();
        services.AddScoped<IClientNetworkInfoRepository, ClientNetworkInfoRepository>();
        services.AddScoped<IClientSessionRepository, ClientSessionRepository>();
        services.AddScoped<IClientBlockHistoryRepository, ClientBlockHistoryRepository>();
        services.AddScoped<IClientHistoryRepository, ClientHistoryRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IUserClientAccessRepository, UserClientAccessRepository>();
        services.AddScoped<IUserGroupAccessRepository, UserGroupAccessRepository>();
        services.AddScoped<IManifestEntryRepository, ManifestEntryRepository>();
        services.AddScoped<IUpdateRequestRepository, UpdateRequestRepository>();
        services.AddScoped<IUpdateDetailRepository, UpdateDetailRepository>();
        services.AddScoped<IFileChangeRepository, FileChangeRepository>();

        return services;
    }

    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IManifestService, ManifestService>();
        services.AddScoped<IStatisticsService, StatisticsService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IUpdateService, UpdateService>();

        services.AddHostedService<FileWatcherService>();
        services.AddHostedService<CleanupService>();

        return services;
    }

    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        services.AddSingleton<PasswordHasher>(sp =>
        {
            var saltRounds = configuration.GetValue<int>("Security:PasswordSaltRounds", 12);
            return new PasswordHasher(saltRounds);
        });

        services.AddSingleton<TokenGenerator>();

        return services;
    }
}