using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UpdateHub.Backend.Tests.TestSupport;
using UpdateHub.BackendServer.Application.BackgroundServices;
using UpdateHub.BackendServer.Domain.Entities.Clients;
using UpdateHub.BackendServer.Domain.Entities.Groups;
using UpdateHub.BackendServer.Domain.Entities.Users;
using UpdateHub.BackendServer.Infrastructure.Configuration;
using UpdateHub.BackendServer.Infrastructure.Database;
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
    /// <param name="backupPath">Каталог копий; по умолчанию — каталог теста.</param>
    /// <param name="intervalHours">Период снятия в часах; ноль отключает копирование.</param>
    /// <returns>Готовая служба.</returns>
    private BackupBackgroundService CreateService(
        int keepCount = 7,
        string? backupPath = null,
        int intervalHours = 24)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => _database.CreateSeparateContext());

        return new BackupBackgroundService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new UpdateHubConfig
            {
                BackupPath = backupPath ?? _backupDirectory,
                BackupKeepCount = keepCount,
                BackupIntervalHours = intervalHours
            }),
            NullLogger<BackupBackgroundService>.Instance);
    }

    /// <summary>Открывает снятую копию как обычную базу.</summary>
    /// <param name="path">Путь к файлу копии.</param>
    /// <returns>Контекст поверх копии.</returns>
    /// <remarks>
    /// <c>Pooling=False</c> здесь обязателен. Без него Microsoft.Data.Sqlite
    /// вернёт соединение в пул, не закрывая файл, и каталог копий не удалится:
    /// в Windows тест падал в <c>Dispose</c>, хотя сама проверка проходила.
    /// </remarks>
    private static AppDbContext OpenBackup(string? path)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False")
            .Options);

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

        using var restored = OpenBackup(path);
        var users = await restored.Users.ToListAsync();

        Assert.Equal("ivanov", Assert.Single(users).Username);
    }

    /// <summary>
    /// В копию попадает вся база, включая связи между таблицами.
    /// </summary>
    /// <remarks>
    /// Проверка отдельно от предыдущей, где читается одна таблица: перенести
    /// схему целиком — отдельное свойство <c>VACUUM INTO</c>. Копия, теряющая
    /// связанные записи, означала бы восстановление с рассыпавшимися правами,
    /// и это хуже явного отказа: беду заметили бы не сразу.
    /// </remarks>
    [Fact]
    public async Task CreateBackupAsync_KeepsRelatedRows()
    {
        var group = new GroupEntity { Name = "Бухгалтерия" };
        _database.Context.Groups.Add(group);
        _database.Context.Clients.Add(new ClientEntity
        {
            Id = "pc-buh-1",
            GroupId = group.Id,
            ComputerInfo = new ClientComputerInfoEntity { Hostname = "buh-1" }
        });
        await _database.Context.SaveChangesAsync();

        var path = await CreateService().CreateBackupAsync();

        using var restored = OpenBackup(path);
        var client = await restored.Clients
            .Include(item => item.Group)
            .Include(item => item.ComputerInfo)
            .SingleAsync();

        Assert.Equal("pc-buh-1", client.Id);
        Assert.Equal("Бухгалтерия", client.Group?.Name);
        Assert.Equal("buh-1", client.ComputerInfo?.Hostname);
    }

    /// <summary>
    /// Апостроф в пути к каталогу копий не ломает команду снятия.
    /// </summary>
    /// <remarks>
    /// <c>VACUUM INTO</c> не принимает параметров, поэтому путь подставляется
    /// прямо в текст запроса, а одинарные кавычки в нём удваиваются. Проверка
    /// держит именно это экранирование: без него апостроф закрыл бы строковый
    /// литерал и запрос перестал бы разбираться. Апостроф в имени папки —
    /// вещь обыденная, и путь приходит из файла настроек, где его может
    /// написать администратор.
    /// </remarks>
    [Fact]
    public async Task CreateBackupAsync_PathWithApostrophe_CreatesFile()
    {
        await SeedAsync();

        // Каталог лежит внутри каталога теста, поэтому убирается вместе с ним.
        var tricky = Path.Combine(_backupDirectory, "kopii d'Artagnan");

        var path = await CreateService(backupPath: tricky).CreateBackupAsync();

        Assert.NotNull(path);
        Assert.Single(Directory.GetFiles(tricky, "updatehub-*.db"));
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

    /// <summary>
    /// Нулевое число хранимых копий отключает уборку, а не удаляет всё.
    /// </summary>
    /// <remarks>
    /// Значение читается двояко — «не хранить ничего» или «не убирать», —
    /// и служба выбирает второе. Удалять копии по настройке, которую легко
    /// обнулить случайно, слишком дорого: это ровно те файлы, ради которых
    /// всё и делается.
    /// </remarks>
    [Fact]
    public async Task CreateBackupAsync_ZeroKeepCount_RemovesNothing()
    {
        await SeedAsync();
        Directory.CreateDirectory(_backupDirectory);

        foreach (var day in new[] { "20260101-000000", "20260102-000000" })
        {
            await File.WriteAllTextAsync(Path.Combine(_backupDirectory, $"updatehub-{day}.db"), "старая");
        }

        await CreateService(keepCount: 0).CreateBackupAsync();

        // Две прежние копии плюс только что снятая.
        Assert.Equal(3, Directory.GetFiles(_backupDirectory, "updatehub-*.db").Length);
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

    /// <summary>
    /// Нулевой период отключает копирование: не создаётся даже каталог.
    /// </summary>
    /// <remarks>
    /// Настройка нужна там, где копиями занимается сама площадка — например,
    /// снимками тома. Проверяется именно раннее возвращение: служба обязана
    /// уйти до создания каталога, иначе выключенное копирование всё равно
    /// оставляло бы следы и первую копию при каждом запуске.
    /// </remarks>
    [Fact]
    public async Task ExecuteAsync_ZeroInterval_MakesNoBackups()
    {
        await SeedAsync();

        var service = CreateService(intervalHours: 0);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.False(Directory.Exists(_backupDirectory));
    }

    /// <summary>Убирает базу и каталог копий.</summary>
    public void Dispose()
    {
        _database.Dispose();
        TempDirectory.Remove(_backupDirectory);

        GC.SuppressFinalize(this);
    }
}
