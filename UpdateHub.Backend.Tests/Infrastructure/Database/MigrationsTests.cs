using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UpdateHub.BackendServer.Application.Repositories.Users;
using UpdateHub.BackendServer.Domain.Entities.Users;
using UpdateHub.BackendServer.Infrastructure.Configuration;
using UpdateHub.BackendServer.Infrastructure.Database;
using UpdateHub.BackendServer.Infrastructure.Security;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Backend.Tests.Infrastructure.Database;

/// <summary>
/// Проверяет миграции базы данных.
/// </summary>
/// <remarks>
/// Миграции — это описание схемы, продублированное вручную рядом с сущностями.
/// Любое расхождение между ними обнаруживается не при сборке, а на боевом
/// сервере при первом же запросе, причём в виде «no such column». Здесь оба
/// описания сверяются друг с другом:
/// <list type="number">
/// <item>снимок модели сравнивается с текущими сущностями средствами EF Core;</item>
/// <item>схема, построенная миграцией, сравнивается со схемой, построенной
/// напрямую по модели, — таблица за таблицей, столбец за столбцом.</item>
/// </list>
/// Если кто-то добавит свойство сущности и забудет миграцию, упадёт первый тест.
/// Если миграция окажется написана не так, как её понимает EF Core, упадёт второй.
/// </remarks>
public class MigrationsTests
{
    /// <summary>Создаёт контекст поверх готового соединения.</summary>
    /// <param name="connection">Открытое соединение с базой в памяти.</param>
    /// <returns>Контекст, работающий с этой базой.</returns>
    private static AppDbContext CreateContext(SqliteConnection connection)
        => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

    /// <summary>Открывает пустую базу в памяти.</summary>
    /// <returns>Открытое соединение; база живёт, пока оно не закрыто.</returns>
    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Снимок модели описывает те же сущности, что и код.
    /// </summary>
    /// <remarks>
    /// <c>HasPendingModelChanges</c> сравнивает модель из
    /// <c>AppDbContextModelSnapshot</c> с моделью, собранной по сущностям
    /// и <c>OnModelCreating</c>. Именно этой проверки не хватает при ручной
    /// правке сущностей: добавить свойство и забыть миграцию слишком легко.
    /// </remarks>
    [Fact]
    public void ModelSnapshot_MatchesCurrentModel()
    {
        using var connection = OpenConnection();
        using var context = CreateContext(connection);

        Assert.False(
            context.Database.HasPendingModelChanges(),
            "Модель изменилась без миграции. Выполните 'dotnet ef migrations add <Название>'");
    }

    /// <summary>
    /// Снимок описывает сущности под теми же именами, что и текущая модель.
    /// </summary>
    /// <remarks>
    /// Проверка появилась после переноса сущностей по подпапкам. Имена типов
    /// записаны в снимке строками, и перенос их меняет. Сравнение схем такую
    /// ошибку не ловит: имена таблиц берутся из <c>DbSet</c> и от пространства
    /// имён не зависят, поэтому снимок с устаревшими именами построит те же
    /// таблицы и пройдёт проверку. Обнаружилось бы это только при следующей
    /// команде <c>dotnet ef migrations add</c> — готовым мусором в миграции.
    /// </remarks>
    [Fact]
    public void ModelSnapshot_DescribesEntitiesUnderCurrentTypeNames()
    {
        using var connection = OpenConnection();
        using var context = CreateContext(connection);

        var current = context.Model.GetEntityTypes()
            .Select(entity => entity.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot!.Model
            .GetEntityTypes()
            .Select(entity => entity.Name)
            .OrderBy(name => name, StringComparer.Ordinal);

        Assert.Equal(current, snapshot);
    }

    /// <summary>
    /// Схема, созданная миграцией, совпадает со схемой, созданной по модели.
    /// </summary>
    /// <remarks>
    /// Проверяются имена таблиц и столбцов, типы, обязательность, первичные
    /// ключи, состав и уникальность индексов, внешние ключи вместе с поведением
    /// при удалении. Сравнение идёт по описанию из <c>PRAGMA</c>, а не по тексту
    /// <c>CREATE TABLE</c>: порядок столбцов в файле миграции роли не играет,
    /// а вот пропущенный <c>ON DELETE SET NULL</c> — играет, и очень.
    /// </remarks>
    [Fact]
    public void MigrationSchema_MatchesSchemaBuiltFromModel()
    {
        using var migrated = OpenConnection();
        using (var context = CreateContext(migrated))
        {
            context.Database.Migrate();
        }

        using var created = OpenConnection();
        using (var context = CreateContext(created))
        {
            context.Database.EnsureCreated();
        }

        Assert.Equal(DescribeSchema(created), DescribeSchema(migrated));
    }

    /// <summary>
    /// После применения миграция записана в журнал: повторный запуск сервера
    /// не станет создавать таблицы заново.
    /// </summary>
    [Fact]
    public void Migrate_RecordsMigrationInHistory()
    {
        using var connection = OpenConnection();
        using var context = CreateContext(connection);

        context.Database.Migrate();

        var applied = context.Database.GetAppliedMigrations().ToList();

        Assert.Equal(context.Database.GetMigrations(), applied);
        Assert.Empty(context.Database.GetPendingMigrations());
    }

    /// <summary>
    /// Каждая миграция названа по образцу «метка времени + название»:
    /// от этого зависит порядок применения.
    /// </summary>
    [Fact]
    public void Migrations_HaveTimestampPrefixedIdentifiers()
    {
        using var connection = OpenConnection();
        using var context = CreateContext(connection);

        var migrations = context.Database.GetMigrations().ToList();

        Assert.NotEmpty(migrations);
        Assert.All(migrations, id => Assert.Matches(@"^\d{14}_[A-Za-z0-9]+$", id));
    }

    /// <summary>
    /// База, созданная прежней версией сервера без миграций, подхватывается,
    /// а не приводит к падению при старте.
    /// </summary>
    /// <remarks>
    /// Это единственный сценарий, в котором ошибка стоит дорого: на площадке
    /// без интернета база уже содержит учётные записи и историю обращений,
    /// и удалить её, чтобы «просто пересоздать», нельзя. Проверяется, что
    /// подготовка проходит без исключения, миграция отмечается применённой,
    /// а ранее заведённый пользователь остаётся на месте.
    /// </remarks>
    [Fact]
    public async Task Initialize_OnDatabaseCreatedWithoutMigrations_AdoptsSchemaAndKeepsData()
    {
        using var connection = OpenConnection();

        // Схема образца прежней версии: создана напрямую по модели, журнала миграций нет.
        using (var legacy = CreateContext(connection))
        {
            await legacy.Database.EnsureCreatedAsync();
            legacy.Users.Add(new UserEntity
            {
                Username = "ivanov",
                PasswordHash = "hash",
                Role = UserRole.Admin
            });
            await legacy.SaveChangesAsync();
        }

        using var context = CreateContext(connection);
        var databaseDirectory = Path.Combine(Path.GetTempPath(), $"updatehub-tests-{Guid.NewGuid():N}");

        try
        {
            var initializer = new DatabaseInitializer(
                context,
                new UserRepository(context),
                new PasswordHasher(workFactor: 4),
                Options.Create(new UpdateHubConfig
                {
                    DatabasePath = Path.Combine(databaseDirectory, "updatehub.db")
                }),
                Options.Create(new BootstrapAdminSettings()),
                NullLogger<DatabaseInitializer>.Instance);

            await initializer.InitializeAsync();

            Assert.Empty(context.Database.GetPendingMigrations());
            Assert.Equal(context.Database.GetMigrations(), context.Database.GetAppliedMigrations());

            // Пользователь на месте, а второго администратора заводить не стали:
            // база не пуста, значит система уже введена в эксплуатацию.
            var users = await context.Users.ToListAsync();
            Assert.Equal("ivanov", Assert.Single(users).Username);
        }
        finally
        {
            if (Directory.Exists(databaseDirectory))
            {
                Directory.Delete(databaseDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Составляет текстовое описание схемы базы.
    /// </summary>
    /// <param name="connection">Соединение с базой.</param>
    /// <returns>
    /// Описание, пригодное для построчного сравнения: таблицы, столбцы,
    /// индексы и внешние ключи в устойчивом порядке.
    /// </returns>
    /// <remarks>
    /// Служебные таблицы EF Core пропускаются: они есть только в базе,
    /// полученной миграцией, и к сравнению схем отношения не имеют. Журнал
    /// применённых миграций создаётся всегда, а таблица замка — на время
    /// применения, чтобы два одновременно запущенных сервера не принялись
    /// править схему разом.
    /// </remarks>
    private static string DescribeSchema(SqliteConnection connection)
    {
        var lines = new List<string>();

        foreach (var table in Query(
            connection,
            "SELECT name FROM sqlite_master WHERE type = 'table' " +
            "AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EFMigrations%' ORDER BY name",
            reader => reader.GetString(0)))
        {
            lines.AddRange(DescribeColumns(connection, table));
            lines.AddRange(DescribeIndexes(connection, table));
            lines.AddRange(DescribeForeignKeys(connection, table));
        }

        lines.Sort(StringComparer.Ordinal);
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>Описывает столбцы таблицы.</summary>
    /// <param name="connection">Соединение с базой.</param>
    /// <param name="table">Имя таблицы.</param>
    /// <returns>По строке на столбец.</returns>
    private static IEnumerable<string> DescribeColumns(SqliteConnection connection, string table)
        => Query(
            connection,
            $"PRAGMA table_info(\"{table}\")",
            reader => $"{table}.столбец {reader.GetString(1)}: тип={reader.GetString(2)} " +
                      $"обязательный={reader.GetBoolean(3)} по-умолчанию={ReadNullableString(reader, 4)} " +
                      $"первичный-ключ={reader.GetInt32(5)}");

    /// <summary>Описывает индексы таблицы вместе с составом столбцов.</summary>
    /// <param name="connection">Соединение с базой.</param>
    /// <param name="table">Имя таблицы.</param>
    /// <returns>По строке на индекс.</returns>
    private static IEnumerable<string> DescribeIndexes(SqliteConnection connection, string table)
    {
        var indexes = Query(
            connection,
            $"PRAGMA index_list(\"{table}\")",
            reader => (Name: reader.GetString(1), Unique: reader.GetBoolean(2), Origin: reader.GetString(3)));

        foreach (var index in indexes)
        {
            // Порядок столбцов в индексе значим: индекс по (ClientId, CreatedAt)
            // обслуживает выборку по компьютеру, а по (CreatedAt, ClientId) — нет.
            var columns = Query(
                connection,
                $"PRAGMA index_info(\"{index.Name}\")",
                reader => (Position: reader.GetInt32(0), Column: ReadNullableString(reader, 2)))
                .OrderBy(x => x.Position)
                .Select(x => x.Column);

            yield return $"{table}.индекс {index.Name}: столбцы=({string.Join(", ", columns)}) " +
                         $"уникальный={index.Unique} источник={index.Origin}";
        }
    }

    /// <summary>Описывает внешние ключи таблицы.</summary>
    /// <param name="connection">Соединение с базой.</param>
    /// <param name="table">Имя таблицы.</param>
    /// <returns>По строке на столбец внешнего ключа.</returns>
    private static IEnumerable<string> DescribeForeignKeys(SqliteConnection connection, string table)
        => Query(
            connection,
            $"PRAGMA foreign_key_list(\"{table}\")",
            reader => $"{table}.внешний-ключ {reader.GetString(3)} -> " +
                      $"{reader.GetString(2)}.{ReadNullableString(reader, 4)} " +
                      $"при-удалении={reader.GetString(6)} при-изменении={reader.GetString(5)}");

    /// <summary>Выполняет запрос и преобразует каждую строку результата.</summary>
    /// <typeparam name="T">Тип результата.</typeparam>
    /// <param name="connection">Соединение с базой.</param>
    /// <param name="sql">Текст запроса.</param>
    /// <param name="read">Преобразование строки результата.</param>
    /// <returns>Прочитанные значения.</returns>
    private static List<T> Query<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> read)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        var results = new List<T>();
        while (reader.Read())
        {
            results.Add(read(reader));
        }

        return results;
    }

    /// <summary>Читает строковое значение, допускающее отсутствие.</summary>
    /// <param name="reader">Читатель результата.</param>
    /// <param name="ordinal">Номер столбца.</param>
    /// <returns>Значение или «нет».</returns>
    private static string ReadNullableString(SqliteDataReader reader, int ordinal)
        => reader.IsDBNull(ordinal) ? "нет" : reader.GetString(ordinal);
}
