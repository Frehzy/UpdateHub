using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Tests.TestSupport;

/// <summary>
/// Одноразовая база данных для теста.
/// </summary>
/// <remarks>
/// Используется настоящий SQLite в памяти, а не поставщик InMemory от EF Core.
/// Разница принципиальна: InMemory не выполняет SQL и потому не проверяет
/// ни уникальные индексы, ни внешние ключи, ни трансляцию запросов в SQL.
/// Половина проверяемого здесь поведения (уникальность пути в манифесте,
/// каскадное удаление, агрегаты статистики) при InMemory прошла бы «зелёной»
/// и сломалась бы на настоящей базе.
/// <para>
/// Соединение держится открытым всё время жизни объекта: база в памяти
/// существует ровно до закрытия последнего соединения к ней.
/// </para>
/// </remarks>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    /// <summary>Создаёт пустую базу со схемой, построенной по модели.</summary>
    public TestDatabase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    /// <summary>Контекст, работающий с этой базой.</summary>
    public AppDbContext Context { get; }

    /// <summary>
    /// Создаёт второй контекст к той же базе.
    /// </summary>
    /// <returns>Новый контекст.</returns>
    /// <remarks>
    /// Нужен там, где важно прочитать данные «свежим взглядом», а не получить
    /// объект из кэша отслеживания первого контекста.
    /// </remarks>
    public AppDbContext CreateSeparateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new AppDbContext(options);
    }

    /// <summary>Закрывает контекст и уничтожает базу.</summary>
    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
