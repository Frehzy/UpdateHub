using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UpdateHub.BackendServer.Application.BackgroundServices;
using UpdateHub.BackendServer.Domain.Entities.Users;
using UpdateHub.BackendServer.Infrastructure.Configuration;
using UpdateHub.BackendServer.Infrastructure.Database;
using UpdateHub.Backend.Tests.TestSupport;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Backend.Tests.Application.BackgroundServices;

/// <summary>
/// Проверяет снятие резервных копий базы.
/// </summary>
/// <remarks>
/// База живёт на единственном томе Docker, и вместе с ней исчезли бы учётные
/// записи, все выданные права и вся история обращений. Восстановить это
/// неоткуда, поэтому проверяется не только сам факт создания файла, но и то,
/// что копия пригодна: из неё читаются те же данные.
/// </remarks>
public class BackupBackgroundServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly string _backupDirectory =
        Path.Combine(Path.GetTempPath(), $"updatehub-backup-{Guid.NewGuid():N}");

    /// <summary>Собирает службу копий поверх базы теста.</summary>
    /// <param name="keepCount">Сколько копий хранить.</param>
    /// <returns>Готовая служба.</returns>
    private BackupBackgroundService CreateService(int keepCount = 7)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _database.CreateSeparateContext());

        return new BackupBackgroundService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new UpdateHubConfig
            {
                BackupPath = _backupDirectory,
                BackupKeepCount = keepCount
            }),
            NullLogger<BackupBackgroundService>.Instance);
    }

    /// <summary>Заводит пользователя, чтобы копии было что содержать.</summary>
    /// <returns>Задача завершения.</returns>
    private async Task SeedAsync()
    {
        _database.Context.Users.Add(new UserEntity
        {
            Username = "ivanov",
            PasswordHash = "hash",
            Role = UserRole.Admin
        });
        await _database.Context.SaveChangesAsync();
    }

    /// <summary>Копия создаётся и оказывается непустой.</summary>
    [Fact]
    public async Task CreateBackupAsync_CreatesFile()
    {
        await SeedAsync();

        var path = await CreateService().CreateBackupAsync();

        Assert.NotNull(path);
        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
    }

    /// <summary>
    /// Из копии читаются те же данные, что были в базе.
    /// </summary>
    /// <remarks>
    /// Ради этого копия снимается командой SQLite, а не копированием файла:
    /// в режиме WAL часть записей лежит в отдельном журнале, и копия файла
    /// оказалась бы без них.
    /// </remarks>
    [Fact]
    public async Task CreateBackupAsync_BackupContainsSameData()
    {
        await SeedAsync();

        var path = await CreateService().CreateBackupAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        using var restored = new AppDbContext(options);
        var users = await restored.Users.ToListAsync();

        Assert.Equal("ivanov", Assert.Single(users).Username);
    }

    /// <summary>Каталог копий создаётся сам, если его ещё нет.</summary>
    [Fact]
    public async Task CreateBackupAsync_CreatesDirectory()
    {
        await SeedAsync();
        Assert.False(Directory.Exists(_backupDirectory));

        await CreateService().CreateBackupAsync();

        Assert.True(Directory.Exists(_backupDirectory));
    }

    /// <summary>
    /// Копии сверх заданного количества удаляются, начиная со старых.
    /// </summary>
    /// <remarks>
    /// Без уборки копии копились бы годами и однажды заполнили диск —
    /// на машине, к которой не ходят, это обнаружилось бы отказом сервера.
    /// </remarks>
    [Fact]
    public async Task CreateBackupAsync_RemovesOutdatedBeyondLimit()
    {
        await SeedAsync();
        Directory.CreateDirectory(_backupDirectory);

        // Копии прошлых дней: имя содержит метку времени, по ней и идёт отбор.
        foreach (var day in new[] { "20260101-000000", "20260102-000000", "20260103-000000" })
        {
            await File.WriteAllTextAsync(Path.Combine(_backupDirectory, $"updatehub-{day}.db"), "старая");
        }

        await CreateService(keepCount: 2).CreateBackupAsync();

        var left = Directory.GetFiles(_backupDirectory, "updatehub-*.db");
        Assert.Equal(2, left.Length);

        // Самая старая уходит первой.
        Assert.DoesNotContain(left, file => file.EndsWith("20260101-000000.db", StringComparison.Ordinal));
    }

    /// <summary>Посторонние файлы в каталоге копий не трогаются.</summary>
    [Fact]
    public async Task CreateBackupAsync_LeavesForeignFilesAlone()
    {
        await SeedAsync();
        Directory.CreateDirectory(_backupDirectory);

        var foreign = Path.Combine(_backupDirectory, "vazhnyy-fayl.txt");
        await File.WriteAllTextAsync(foreign, "не трогать");

        await CreateService(keepCount: 1).CreateBackupAsync();

        Assert.True(File.Exists(foreign));
    }

    /// <summary>Убирает базу и каталог копий.</summary>
    public void Dispose()
    {
        _database.Dispose();

        if (Directory.Exists(_backupDirectory))
        {
            Directory.Delete(_backupDirectory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
