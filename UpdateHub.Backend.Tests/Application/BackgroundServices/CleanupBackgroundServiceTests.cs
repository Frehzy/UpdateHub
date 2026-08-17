using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UpdateHub.Backend.Tests.TestSupport;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Clients;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Manifest;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Updates;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Users;
using UpdateHub.BackendServer.Application.BackgroundServices;
using UpdateHub.BackendServer.Application.Repositories.Clients;
using UpdateHub.BackendServer.Application.Repositories.Manifest;
using UpdateHub.BackendServer.Application.Repositories.Updates;
using UpdateHub.BackendServer.Application.Repositories.Users;
using UpdateHub.BackendServer.Domain.Entities.Clients;
using UpdateHub.BackendServer.Domain.Entities.Updates;
using UpdateHub.BackendServer.Domain.Entities.Users;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.BackendServer.Infrastructure.Configuration;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Backend.Tests.Application.BackgroundServices;

/// <summary>
/// Проверяет ежесуточную очистку устаревших записей.
/// </summary>
/// <remarks>
/// Единственная служба, которая удаляет данные без спроса, и до появления этих
/// проверок она не была покрыта ничем. Ошибка здесь необратима: истории обращений
/// и изменений файлов взять больше негде, а сервер стоит в контуре, к которому
/// не ходят, — исчезновение записей обнаружилось бы через полгода, когда они
/// понадобятся для разбора.
/// <para>
/// Поэтому проверяется не только то, что старое удаляется, но и то, что свежее
/// остаётся: перепутанный знак в границе прошёл бы проверку «старое удалено»
/// и снёс бы всё.
/// </para>
/// </remarks>
public class CleanupBackgroundServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();

    /// <summary>
    /// Собирает службу очистки поверх базы теста.
    /// </summary>
    /// <param name="requestRetentionDays">Срок хранения обращений в сутках.</param>
    /// <param name="historyRetentionDays">Срок хранения истории в сутках.</param>
    /// <returns>Готовая служба.</returns>
    private CleanupBackgroundService CreateService(
        int requestRetentionDays = 30,
        int historyRetentionDays = 180)
    {
        var services = new ServiceCollection();

        // Все репозитории смотрят в одну базу теста: служба берёт их из области
        // зависимостей, как и в работе.
        services.AddScoped<IUpdateRequestRepository>(_ => new UpdateRequestRepository(_database.Context));
        services.AddScoped<IFileChangeRepository>(_ => new FileChangeRepository(_database.Context));
        services.AddScoped<IClientHistoryRepository>(_ => new ClientHistoryRepository(_database.Context));
        services.AddScoped<IRefreshTokenRepository>(_ => new RefreshTokenRepository(_database.Context));
        services.AddScoped<IClientNetworkInfoRepository>(_ => new ClientNetworkInfoRepository(_database.Context));

        return new CleanupBackgroundService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new UpdateHubConfig
            {
                RequestRetentionDays = requestRetentionDays,
                HistoryRetentionDays = historyRetentionDays
            }),
            NullLogger<CleanupBackgroundService>.Instance);
    }

    /// <summary>Заводит компьютер, на который ссылаются журнальные записи.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    private async Task AddClientAsync(string clientId)
    {
        _database.Context.Clients.Add(new ClientEntity { Id = clientId, IsActive = true });
        await _database.Context.SaveChangesAsync();
    }

    /// <summary>Заводит обращение заданной давности.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="daysAgo">Сколько суток назад состоялось обращение.</param>
    private async Task AddRequestAgoAsync(string clientId, double daysAgo)
    {
        _database.Context.UpdateRequests.Add(new UpdateRequestEntity
        {
            ClientId = clientId,
            RequestTimestamp = DateTime.UtcNow.AddDays(-daysAgo)
        });

        await _database.Context.SaveChangesAsync();
    }

    /// <summary>Заводит запись истории компьютера заданной давности.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="daysAgo">Сколько суток назад произошло изменение.</param>
    private async Task AddHistoryAgoAsync(string clientId, double daysAgo)
    {
        _database.Context.ClientHistories.Add(new ClientHistoryEntity
        {
            ClientId = clientId,
            ChangeType = ClientChangeType.Blocked,
            ChangeTimestamp = DateTime.UtcNow.AddDays(-daysAgo)
        });

        await _database.Context.SaveChangesAsync();
    }

    /// <summary>Заводит пользователя и токен обновления с заданным сроком.</summary>
    /// <param name="expiresInDays">Через сколько суток истекает; отрицательное — уже истёк.</param>
    private async Task AddRefreshTokenAsync(double expiresInDays)
    {
        var user = new UserEntity
        {
            Username = $"user-{Guid.NewGuid():N}",
            PasswordHash = "hash",
            Role = UserRole.Client
        };

        _database.Context.Users.Add(user);
        _database.Context.RefreshTokens.Add(new RefreshTokenEntity
        {
            UserId = user.Id,
            Token = Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(expiresInDays)
        });

        await _database.Context.SaveChangesAsync();
    }

    /// <summary>Обращения старше срока хранения удаляются.</summary>
    [Fact]
    public async Task RunCleanupAsync_OldRequests_Deleted()
    {
        await AddClientAsync("pc-1");
        await AddRequestAgoAsync("pc-1", daysAgo: 40);

        await CreateService(requestRetentionDays: 30).RunCleanupAsync();

        using var context = _database.CreateSeparateContext();
        Assert.Empty(context.UpdateRequests);
    }

    /// <summary>
    /// Обращения в пределах срока хранения остаются.
    /// </summary>
    /// <remarks>
    /// Половина проверки, без которой первая ничего не стоит: перепутанный знак
    /// в вычислении границы удалил бы всё подряд и «удаление старого» прошло бы.
    /// </remarks>
    [Fact]
    public async Task RunCleanupAsync_RecentRequests_Kept()
    {
        await AddClientAsync("pc-1");
        await AddRequestAgoAsync("pc-1", daysAgo: 5);

        await CreateService(requestRetentionDays: 30).RunCleanupAsync();

        using var context = _database.CreateSeparateContext();
        Assert.Single(context.UpdateRequests);
    }

    /// <summary>
    /// История живёт по своему сроку, более длительному, чем у обращений.
    /// </summary>
    /// <remarks>
    /// Сроки разные не случайно: обращения копятся ежедневно и по каждой машине,
    /// а история изменений — редкая и нужна дольше. Перепутать две настройки
    /// легко, и тогда история исчезала бы через месяц вместо полугода.
    /// </remarks>
    [Fact]
    public async Task RunCleanupAsync_HistoryKeptByItsOwnRetention()
    {
        await AddClientAsync("pc-1");
        await AddHistoryAgoAsync("pc-1", daysAgo: 60);

        await CreateService(requestRetentionDays: 30, historyRetentionDays: 180).RunCleanupAsync();

        using var context = _database.CreateSeparateContext();
        Assert.Single(context.ClientHistories);
    }

    /// <summary>История старше своего срока удаляется.</summary>
    [Fact]
    public async Task RunCleanupAsync_OldHistory_Deleted()
    {
        await AddClientAsync("pc-1");
        await AddHistoryAgoAsync("pc-1", daysAgo: 200);

        await CreateService(historyRetentionDays: 180).RunCleanupAsync();

        using var context = _database.CreateSeparateContext();
        Assert.Empty(context.ClientHistories);
    }

    /// <summary>
    /// Нулевой срок хранения отключает удаление, а не сносит всё.
    /// </summary>
    /// <remarks>
    /// Прежде защиты не было: при нуле граница приходилась на текущий момент,
    /// и ближайшая ночная очистка молча удаляла всю историю обращений —
    /// необратимо, на сервере, к которому никто не ходит. Ноль как «отключено» —
    /// то же соглашение, что у BackupIntervalHours и BackupKeepCount: опечатка
    /// в настройке не должна уничтожать данные.
    /// </remarks>
    [Fact]
    public async Task RunCleanupAsync_ZeroRetention_DeletesNothing()
    {
        await AddClientAsync("pc-1");
        await AddRequestAgoAsync("pc-1", daysAgo: 1000);
        await AddHistoryAgoAsync("pc-1", daysAgo: 1000);

        await CreateService(requestRetentionDays: 0, historyRetentionDays: 0).RunCleanupAsync();

        using var context = _database.CreateSeparateContext();
        Assert.Single(context.UpdateRequests);
        Assert.Single(context.ClientHistories);
    }

    /// <summary>Отрицательный срок хранения тоже ничего не удаляет.</summary>
    /// <remarks>
    /// Отдельно от нуля: отрицательное значение сдвигало границу в будущее,
    /// то есть удаляло записи ещё увереннее.
    /// </remarks>
    [Fact]
    public async Task RunCleanupAsync_NegativeRetention_DeletesNothing()
    {
        await AddClientAsync("pc-1");
        await AddRequestAgoAsync("pc-1", daysAgo: 1);

        await CreateService(requestRetentionDays: -5, historyRetentionDays: -5).RunCleanupAsync();

        using var context = _database.CreateSeparateContext();
        Assert.Single(context.UpdateRequests);
    }

    /// <summary>
    /// Просроченные токены обновления удаляются, действующие остаются.
    /// </summary>
    /// <remarks>
    /// Эти удаляются независимо от сроков хранения: срок жизни задан при выдаче,
    /// и держать недействительный токен незачем. Но действующий удалять нельзя —
    /// иначе машины теряли бы вход в три часа ночи.
    /// </remarks>
    [Fact]
    public async Task RunCleanupAsync_ExpiredTokensDeleted_ValidKept()
    {
        await AddRefreshTokenAsync(expiresInDays: -1);
        await AddRefreshTokenAsync(expiresInDays: 30);

        await CreateService().RunCleanupAsync();

        using var context = _database.CreateSeparateContext();
        var left = await context.RefreshTokens.ToListAsync();

        Assert.Single(left);
        Assert.True(left[0].ExpiresAt > DateTime.UtcNow);
    }

    /// <summary>
    /// Просроченные токены удаляются даже при отключённой очистке журналов.
    /// </summary>
    [Fact]
    public async Task RunCleanupAsync_ZeroRetention_StillRemovesExpiredTokens()
    {
        await AddRefreshTokenAsync(expiresInDays: -1);

        await CreateService(requestRetentionDays: 0, historyRetentionDays: 0).RunCleanupAsync();

        using var context = _database.CreateSeparateContext();
        Assert.Empty(context.RefreshTokens);
    }

    /// <summary>Освобождает базу.</summary>
    public void Dispose()
    {
        _database.Dispose();
        GC.SuppressFinalize(this);
    }
}
