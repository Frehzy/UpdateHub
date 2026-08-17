using Microsoft.EntityFrameworkCore.Design;

namespace UpdateHub.BackendServer.Infrastructure.Database;

/// <summary>
/// Создаёт контекст для инструментов <c>dotnet ef</c> во время разработки.
/// </summary>
/// <remarks>
/// Используется только командами вида <c>dotnet ef migrations add</c>
/// и не участвует в работе приложения. Путь к базе задаётся переменной
/// окружения <c>UpdateHub__DatabasePath</c>, иначе берётся локальный каталог.
/// </remarks>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>Создаёт контекст с настройками времени разработки.</summary>
    /// <param name="args">Аргументы командной строки инструмента.</param>
    /// <returns>Готовый контекст.</returns>
    public AppDbContext CreateDbContext(string[] args)
    {
        var path = Environment.GetEnvironmentVariable("UpdateHub__DatabasePath")
                   ?? Path.Combine(Directory.GetCurrentDirectory(), "data", "updatehub.db");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={path}");

        return new AppDbContext(optionsBuilder.Options);
    }
}
