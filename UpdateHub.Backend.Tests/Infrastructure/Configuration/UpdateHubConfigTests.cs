using UpdateHub.BackendServer.Infrastructure.Configuration;

namespace UpdateHub.Backend.Tests.Infrastructure.Configuration;

/// <summary>
/// Проверяет приведение путей из конфигурации к абсолютному виду.
/// </summary>
/// <remarks>
/// Раньше относительные пути разрешались от текущего каталога процесса,
/// который задаёт запускающая сторона: Visual Studio ставит его в корень
/// проекта, служба Windows — в системный каталог. Одна и та же настройка
/// приводила к разным папкам, и понять, откуда сервер раздаёт файлы,
/// было невозможно.
/// </remarks>
public class UpdateHubConfigTests
{
    /// <summary>
    /// Относительный путь разрешается от каталога сборки, а не от текущего
    /// каталога процесса.
    /// </summary>
    [Fact]
    public void Resolve_RelativePath_ResolvedAgainstBaseDirectory()
    {
        var resolved = UpdateHubConfig.Resolve("files");

        Assert.StartsWith(
            AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),
            resolved,
            StringComparison.Ordinal);
        Assert.True(Path.IsPathRooted(resolved));
    }

    /// <summary>Путь с ведущим «./» разрешается так же, как без него.</summary>
    [Fact]
    public void Resolve_PathWithLeadingDot_EqualsPathWithout()
    {
        Assert.Equal(UpdateHubConfig.Resolve("files"), UpdateHubConfig.Resolve("./files"));
    }

    /// <summary>
    /// Абсолютный путь остаётся неизменным: в Docker пути задаются абсолютными,
    /// и подменять их каталогом сборки нельзя.
    /// </summary>
    [Fact]
    public void Resolve_AbsolutePath_LeftUnchanged()
    {
        var absolute = OperatingSystem.IsWindows() ? @"C:\updatehub\files" : "/app/files";

        var resolved = UpdateHubConfig.Resolve(absolute);

        Assert.Equal(Path.GetFullPath(absolute), resolved);
    }

    /// <summary>Вложенный относительный путь сохраняет структуру подкаталогов.</summary>
    [Fact]
    public void Resolve_NestedRelativePath_KeepsStructure()
    {
        var resolved = UpdateHubConfig.Resolve("data/updatehub.db");

        Assert.EndsWith("updatehub.db", resolved, StringComparison.Ordinal);
        Assert.Contains("data", resolved, StringComparison.Ordinal);
    }

    /// <summary>
    /// Пустое значение не приводит к исключению: сервер должен подсказать,
    /// что настройка не заполнена, а не упасть при старте с невнятной ошибкой.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Resolve_EmptyValue_ReturnsBaseDirectory(string? path)
    {
        var resolved = UpdateHubConfig.Resolve(path!);

        Assert.True(Path.IsPathRooted(resolved));
    }

    /// <summary>Свойства с готовыми путями согласованы с методом приведения.</summary>
    [Fact]
    public void ResolvedPathProperties_MatchResolveMethod()
    {
        var config = new UpdateHubConfig { FilesPath = "files", DatabasePath = "data/updatehub.db" };

        Assert.Equal(UpdateHubConfig.Resolve("files"), config.ResolvedFilesPath);
        Assert.Equal(UpdateHubConfig.Resolve("data/updatehub.db"), config.ResolvedDatabasePath);
    }

    /// <summary>
    /// Значения по умолчанию рассчитаны на Docker: там образ монтирует папку
    /// Windows в <c>/app/files</c>, а базу держит на именованном томе.
    /// </summary>
    [Fact]
    public void DefaultValues_TargetDockerLayout()
    {
        var config = new UpdateHubConfig();

        Assert.Equal("/app/files", config.FilesPath);
        Assert.Equal("/app/data/updatehub.db", config.DatabasePath);
    }
}
