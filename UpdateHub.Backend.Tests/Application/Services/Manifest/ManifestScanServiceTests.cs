using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UpdateHub.Backend.Tests.TestSupport;
using UpdateHub.BackendServer.Application.Manifest;
using UpdateHub.BackendServer.Application.Repositories.Manifest;
using UpdateHub.BackendServer.Application.Repositories;
using UpdateHub.BackendServer.Application.Services.Manifest;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.BackendServer.Domain.ValueObjects;
using UpdateHub.BackendServer.Infrastructure.Configuration;

namespace UpdateHub.Backend.Tests.Application.Services.Manifest;

/// <summary>
/// Проверяет обход каталога раздачи и построение эталонного манифеста.
/// </summary>
/// <remarks>
/// Работает с настоящим временным каталогом на диске: проверяемое поведение
/// целиком завязано на файловую систему — время изменения файла, его размер,
/// различие имён по регистру. Подменять её здесь означало бы проверять
/// подделку вместо настоящего кода.
/// </remarks>
public class ManifestScanServiceTests : IDisposable
{
    /// <summary>Сколько секунд файл должен пролежать без изменений.</summary>
    private const int SettleSeconds = 5;

    private readonly TestDatabase _database;
    private readonly string _filesPath;
    private readonly ManifestState _state;
    private readonly ManifestScanService _service;

    /// <summary>Готовит временный каталог, базу и службу.</summary>
    public ManifestScanServiceTests()
    {
        _database = new TestDatabase();
        _filesPath = Path.Combine(Path.GetTempPath(), "updatehub-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_filesPath);

        _state = new ManifestState();
        _service = new ManifestScanService(
            Options.Create(new UpdateHubConfig
            {
                FilesPath = _filesPath,
                FileSettleSeconds = SettleSeconds,
                Md5BufferSizeBytes = 4096
            }),
            _state,
            new ManifestEntryRepository(_database.Context),
            new FileChangeRepository(_database.Context),
            NullLogger<ManifestScanService>.Instance);
    }

    /// <summary>
    /// Создаёт файл и назначает ему возраст.
    /// </summary>
    /// <param name="relativePath">Путь относительно каталога раздачи.</param>
    /// <param name="content">Содержимое файла.</param>
    /// <param name="ageSeconds">
    /// На сколько секунд в прошлое сдвинуть время изменения. Значение больше
    /// порога «отстаивания» делает файл готовым к обработке, меньше — отложенным.
    /// Время задаётся явно, чтобы тест не зависел от скорости машины.
    /// </param>
    /// <remarks>
    /// Кодировка не указывается намеренно: перегрузка без неё пишет UTF-8
    /// без метки порядка байтов. С явным <c>Encoding.UTF8</c> метка попадала бы
    /// в файл, меняя и его размер, и контрольную сумму.
    /// </remarks>
    private void WriteFile(string relativePath, string content, int ageSeconds = 60)
    {
        var fullPath = Path.Combine(_filesPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow.AddSeconds(-ageSeconds));
    }

    /// <summary>Читает манифест из базы отдельным контекстом.</summary>
    /// <returns>Словарь «путь — контрольная сумма».</returns>
    private Dictionary<string, string> ReadManifest()
    {
        using var context = _database.CreateSeparateContext();
        return context.ManifestEntries.ToDictionary(e => e.RelativePath, e => e.Md5Hash);
    }

    /// <summary>Обычный файл попадает в манифест с верной контрольной суммой.</summary>
    [Fact]
    public async Task ScanAsync_PlainFile_AddedToManifest()
    {
        WriteFile("a.txt", "hello");

        var result = await _service.ScanAsync();

        Assert.True(result.Executed);
        Assert.Equal(1, result.TotalFiles);

        // MD5 строки "hello" — известное значение, его удобно проверить напрямую.
        Assert.Equal("5d41402abc4b2a76b9719d911017c592", ReadManifest()["a.txt"]);
    }

    /// <summary>Файлы во вложенных каталогах попадают в манифест с прямыми слэшами.</summary>
    [Fact]
    public async Task ScanAsync_NestedDirectory_PathUsesForwardSlashes()
    {
        WriteFile("bin/tools/app.bin", "содержимое");

        await _service.ScanAsync();

        Assert.True(ReadManifest().ContainsKey("bin/tools/app.bin"));
    }

    /// <summary>
    /// Только что изменённый файл откладывается до следующего обхода.
    /// Без этого в манифест попала бы сумма наполовину скопированного файла,
    /// и клиенты перекачивали бы его бесконечно, никогда не сходясь по сумме.
    /// </summary>
    [Fact]
    public async Task ScanAsync_RecentlyModifiedFile_Deferred()
    {
        WriteFile("копируется.iso", "часть данных", ageSeconds: 0);

        var result = await _service.ScanAsync();

        Assert.Equal(0, result.TotalFiles);
        Assert.Empty(ReadManifest());
    }

    /// <summary>
    /// Отложенный файл не считается исчезнувшим и не пропадает из манифеста.
    /// Иначе на время перезаписи файл выпадал бы из выдачи, и клиенты
    /// получали бы отказ при попытке его скачать.
    /// </summary>
    [Fact]
    public async Task ScanAsync_FileBeingRewritten_StaysInManifest()
    {
        WriteFile("a.txt", "старое содержимое");
        await _service.ScanAsync();
        Assert.Single(ReadManifest());

        // Файл начали перезаписывать: время изменения стало текущим.
        WriteFile("a.txt", "новое содержимое, ещё пишется", ageSeconds: 0);
        await _service.ScanAsync();

        var manifest = ReadManifest();
        Assert.Single(manifest);
        Assert.Equal("a.txt", manifest.Keys.Single());
    }

    /// <summary>
    /// Повторный обход не пересчитывает MD5, если размер и время не менялись.
    /// Это главная оптимизация: без неё каждый обход читал бы шестигигабайтный
    /// образ целиком через медленный проброс папки Windows.
    /// </summary>
    [Fact]
    public async Task ScanAsync_SecondScanWithoutChanges_SkipsHashing()
    {
        WriteFile("a.txt", "hello");
        WriteFile("b.txt", "world");

        var first = await _service.ScanAsync();
        Assert.Equal(2, first.HashedFiles);

        var second = await _service.ScanAsync();

        Assert.Equal(2, second.TotalFiles);
        Assert.Equal(0, second.HashedFiles);
        Assert.Equal(0, second.Changes);
    }

    /// <summary>Изменённое содержимое обновляет сумму в манифесте.</summary>
    [Fact]
    public async Task ScanAsync_ContentChanged_HashUpdated()
    {
        WriteFile("a.txt", "hello");
        await _service.ScanAsync();

        WriteFile("a.txt", "другое содержимое");
        var result = await _service.ScanAsync();

        Assert.Equal(1, result.HashedFiles);
        Assert.NotEqual("5d41402abc4b2a76b9719d911017c592", ReadManifest()["a.txt"]);
    }

    /// <summary>Удалённый с диска файл убирается из манифеста.</summary>
    [Fact]
    public async Task ScanAsync_FileDeletedFromDisk_RemovedFromManifest()
    {
        WriteFile("a.txt", "hello");
        WriteFile("b.txt", "world");
        await _service.ScanAsync();

        File.Delete(Path.Combine(_filesPath, "a.txt"));
        await _service.ScanAsync();

        var manifest = ReadManifest();
        Assert.Single(manifest);
        Assert.True(manifest.ContainsKey("b.txt"));
    }

    /// <summary>
    /// Имена, различающиеся только регистром, отбрасываются целиком.
    /// На NTFS сервера они неразличимы, на ext4 клиента это два разных файла;
    /// выбор одного из них зависел бы от порядка обхода каталога.
    /// </summary>
    [Fact]
    public async Task ScanAsync_CaseCollision_BothSidesRejected()
    {
        WriteFile("Doc.txt", "первый");

        // На файловых системах, не различающих регистр, второй файл просто
        // перезапишет первый — тогда конфликта нет и проверять нечего.
        if (File.Exists(Path.Combine(_filesPath, "doc.txt")))
        {
            return;
        }

        WriteFile("doc.txt", "второй");

        var result = await _service.ScanAsync();

        Assert.Empty(ReadManifest());
        Assert.Equal(2, result.RejectedPaths.Count);
        Assert.All(result.RejectedPaths, p => Assert.Contains("регистр", p, StringComparison.Ordinal));
    }

    /// <summary>Отсутствующий каталог создаётся, а обход завершается без ошибки.</summary>
    [Fact]
    public async Task ScanAsync_MissingDirectory_CreatedAndScanSucceeds()
    {
        Directory.Delete(_filesPath, recursive: true);

        var result = await _service.ScanAsync();

        Assert.True(result.Executed);
        Assert.True(Directory.Exists(_filesPath));
        Assert.Equal(0, result.TotalFiles);
    }

    /// <summary>Итоги обхода попадают в общее состояние манифеста.</summary>
    [Fact]
    public async Task ScanAsync_UpdatesSharedState()
    {
        WriteFile("a.txt", "hello");

        await _service.ScanAsync();

        Assert.Equal(1, _state.EntryCount);
        Assert.Equal(5, _state.TotalSizeBytes);
        Assert.NotNull(_state.LastScanCompletedAt);
        Assert.Equal(1, _state.Generation);
    }

    /// <summary>
    /// Обход без изменений не увеличивает поколение: номер должен означать
    /// «содержимое каталога поменялось», а не «прошёл очередной опрос».
    /// </summary>
    [Fact]
    public async Task ScanAsync_ScanWithoutChanges_GenerationUnchanged()
    {
        WriteFile("a.txt", "hello");
        await _service.ScanAsync();
        var generation = _state.Generation;

        await _service.ScanAsync();

        Assert.Equal(generation, _state.Generation);
    }

    /// <summary>История изменений файлов пополняется при добавлении и удалении.</summary>
    [Fact]
    public async Task ScanAsync_ChangesRecordedInHistory()
    {
        WriteFile("a.txt", "hello");
        await _service.ScanAsync();

        File.Delete(Path.Combine(_filesPath, "a.txt"));
        await _service.ScanAsync();

        using var context = _database.CreateSeparateContext();
        var changes = context.FileChanges.OrderBy(c => c.Id).ToList();

        Assert.Equal(2, changes.Count);
        Assert.Equal(FileChangeType.Created, changes[0].ChangeType);
        Assert.Equal(FileChangeType.Deleted, changes[1].ChangeType);
    }

    /// <summary>Освобождает базу и временный каталог.</summary>
    public void Dispose()
    {
        _database.Dispose();

        try
        {
            if (Directory.Exists(_filesPath))
            {
                Directory.Delete(_filesPath, recursive: true);
            }
        }
        catch (IOException)
        {
            // Уборка временного каталога не должна ронять прогон тестов.
        }

        GC.SuppressFinalize(this);
    }
}
