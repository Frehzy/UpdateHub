using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UpdateHub.Backend.Tests.TestSupport;
using UpdateHub.BackendServer.Application.Repositories.Users;
using UpdateHub.BackendServer.Domain.Entities.Users;
using UpdateHub.BackendServer.Infrastructure.Configuration;
using UpdateHub.BackendServer.Infrastructure.Database;
using UpdateHub.BackendServer.Infrastructure.Diagnostics;
using UpdateHub.BackendServer.Infrastructure.Security;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Backend.Tests.Infrastructure.Database;

/// <summary>
/// Проверяет, что снятая копия действительно годится на замену рабочей базе.
/// </summary>
/// <remarks>
/// Копии снимались, но ни одна ни разу не подставлялась вместо живой базы —
/// а копия, которую никогда не восстанавливали, это надежда, а не копия.
/// Порядок восстановления описан в docs/vosstanovlenie-iz-kopii.md, здесь
/// проверяется его существенная часть.
/// <para>
/// Главное, что могло пойти не так: восстановленный файл не содержит журнала
/// применённых миграций, и подготовка базы при старте принимает его за базу
/// прежней версии либо за пустую. В первом случае она пыталась бы создать
/// таблицы заново и упала бы на «table Users already exists», во втором —
/// завела бы второго администратора со случайным паролем поверх работающей
/// системы. И то и другое обнаружилось бы на площадке без интернета, когда
/// восстановление уже начато и отступать некуда.
/// </para>
/// </remarks>
public class BackupRestoreTests : IDisposable
{
    private readonly string _workDirectory =
        Path.Combine(Path.GetTempPath(), $"updatehub-restore-{Guid.NewGuid():N}");

    /// <summary>Открывает базу по пути к файлу.</summary>
    /// <param name="path">Путь к файлу базы.</param>
    /// <returns>Контекст поверх файла.</returns>
    /// <remarks>
    /// <c>Pooling=False</c> обязателен: иначе Microsoft.Data.Sqlite оставит
    /// соединение в пуле, файл останется открытым и каталог не удалится —
    /// в Windows уборка теста падала бы уже после успешной проверки.
    /// </remarks>
    private static AppDbContext OpenFile(string path)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={path};Pooling=False")
            .Options);

    /// <summary>Готовит базу к работе так же, как это делает сервер при старте.</summary>
    /// <param name="context">Контекст базы.</param>
    /// <param name="path">Путь к файлу базы.</param>
    /// <returns>Задача завершения.</returns>
    private static Task InitializeAsync(AppDbContext context, string path)
    {
        var initializer = new DatabaseInitializer(
            context,
            new UserRepository(context),
            new PasswordHasher(workFactor: 4),
            Options.Create(new UpdateHubConfig { DatabasePath = path }),
            Options.Create(new BootstrapAdminSettings()),
            new BootstrapReport(),
            NullLogger<DatabaseInitializer>.Instance);

        return initializer.InitializeAsync();
    }

    /// <summary>
    /// Восстановленная копия подхватывается при старте: данные на месте,
    /// схема считается актуальной, второй администратор не заводится.
    /// </summary>
    [Fact]
    public async Task RestoredBackup_IsAdoptedOnStartup()
    {
        Directory.CreateDirectory(_workDirectory);

        var livePath = Path.Combine(_workDirectory, "updatehub.db");
        var backupPath = Path.Combine(_workDirectory, "updatehub-kopiya.db");

        // Рабочая база: подготовлена как на сервере, с одним пользователем.
        using (var live = OpenFile(livePath))
        {
            await InitializeAsync(live, livePath);

            live.Users.Add(new UserEntity
            {
                Username = "petrov",
                PasswordHash = "hash",
                Role = UserRole.Client
            });
            await live.SaveChangesAsync();

            var quoted = backupPath.Replace("'", "''");
#pragma warning disable EF1002
            await live.Database.ExecuteSqlRawAsync($"VACUUM INTO '{quoted}'");
#pragma warning restore EF1002
        }

        Assert.True(File.Exists(backupPath));

        // Восстановление: копия занимает место рабочей базы. Ровно это делает
        // администратор по описанной в документации последовательности.
        // Вместе с базой убираются её журналы WAL. Иначе восстановленный файл
        // окажется рядом с журналом от прежней базы, и SQLite при открытии
        // попытается применить чужие незаписанные страницы. Тот же порядок
        // указан в docs/vosstanovlenie-iz-kopii.md.
        foreach (var leftover in new[] { livePath, livePath + "-wal", livePath + "-shm" })
        {
            if (File.Exists(leftover))
            {
                File.Delete(leftover);
            }
        }

        File.Move(backupPath, livePath);

        using var restored = OpenFile(livePath);
        await InitializeAsync(restored, livePath);

        // Схема считается актуальной: журнал миграций перенесён копией.
        Assert.Empty(restored.Database.GetPendingMigrations());
        Assert.Equal(restored.Database.GetMigrations(), restored.Database.GetAppliedMigrations());

        // Учётные записи на месте, и второго администратора не появилось:
        // база не пуста, значит система уже введена в эксплуатацию.
        var users = await restored.Users.ToListAsync();
        Assert.Equal(["admin", "petrov"], users.Select(user => user.Username).OrderBy(name => name, StringComparer.Ordinal));
    }

    /// <summary>
    /// Копия содержит записи, добавленные незадолго до её снятия.
    /// </summary>
    /// <remarks>
    /// Ради этого копия снимается командой SQLite, а не копированием файла:
    /// в режиме WAL свежие записи лежат в отдельном журнале, и копия файла
    /// оказалась бы без них — то есть потеряла бы именно то, что нужнее всего.
    /// Здесь режим WAL включает сама подготовка базы, как и на сервере.
    /// </remarks>
    [Fact]
    public async Task Backup_IncludesRowsWrittenInWalMode()
    {
        Directory.CreateDirectory(_workDirectory);

        var livePath = Path.Combine(_workDirectory, "updatehub.db");
        var backupPath = Path.Combine(_workDirectory, "updatehub-kopiya.db");

        using (var live = OpenFile(livePath))
        {
            await InitializeAsync(live, livePath);

            // PRAGMA читается через обычное соединение: SqlQueryRaw ожидает
            // столбец с именем Value, а pragma возвращает своё.
            var connection = live.Database.GetDbConnection();
            await connection.OpenAsync();

            string? mode;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA journal_mode";
                mode = (string?)await command.ExecuteScalarAsync();
            }

            Assert.Equal("wal", mode?.ToLowerInvariant());

            live.Users.Add(new UserEntity
            {
                Username = "svezhiy",
                PasswordHash = "hash",
                Role = UserRole.Client
            });
            await live.SaveChangesAsync();

            var quoted = backupPath.Replace("'", "''");
#pragma warning disable EF1002
            await live.Database.ExecuteSqlRawAsync($"VACUUM INTO '{quoted}'");
#pragma warning restore EF1002
        }

        using var backup = OpenFile(backupPath);
        var names = await backup.Users.Select(user => user.Username).ToListAsync();

        Assert.Contains("svezhiy", names);
    }

    /// <summary>Убирает рабочий каталог.</summary>
    public void Dispose()
    {
        TempDirectory.Remove(_workDirectory);
        GC.SuppressFinalize(this);
    }
}
