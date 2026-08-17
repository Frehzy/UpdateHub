using Microsoft.Extensions.Logging.Abstractions;
using UpdateHub.Backend.Tests.TestSupport;
using UpdateHub.BackendServer.Application.Manifest;
using UpdateHub.BackendServer.Application.Repositories;
using UpdateHub.BackendServer.Application.Services;
using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Domain.Entities;
using UpdateHub.BackendServer.Domain.Enums;

namespace UpdateHub.Backend.Tests.Application.Services;

/// <summary>
/// Проверяет сравнение манифеста клиента с эталонным.
/// </summary>
/// <remarks>
/// Здесь решается, что именно клиент будет качать по каналу 2 Мбит/с.
/// Лишний файл в плане — это часы напрасной передачи, пропущенный —
/// необновлённая машина.
/// </remarks>
public class SyncServiceTests : IDisposable
{
    private readonly TestDatabase _database;
    private readonly ManifestState _state;
    private readonly SyncService _service;

    /// <summary>Готовит базу и службу.</summary>
    public SyncServiceTests()
    {
        _database = new TestDatabase();
        _state = new ManifestState();
        _service = new SyncService(
            new ManifestEntryRepository(_database.Context),
            _state,
            NullLogger<SyncService>.Instance);
    }

    /// <summary>Добавляет запись в эталонный манифест.</summary>
    /// <param name="path">Путь файла.</param>
    /// <param name="md5">Контрольная сумма.</param>
    /// <param name="size">Размер в байтах.</param>
    private async Task AddServerFileAsync(string path, string md5, long size = 100)
    {
        _database.Context.ManifestEntries.Add(new ManifestEntryEntity
        {
            RelativePath = path,
            Md5Hash = md5,
            SizeBytes = size,
            LastModified = DateTime.UtcNow
        });

        await _database.Context.SaveChangesAsync();
    }

    /// <summary>Собирает запрос на сравнение.</summary>
    /// <param name="clientManifest">Манифест клиента.</param>
    /// <returns>Запрос.</returns>
    private static SyncRequest CreateRequest(Dictionary<string, string> clientManifest)
        => new("pc-1", "ivanov", RequestType.Sync, clientManifest);

    /// <summary>
    /// Совпадающие суммы означают, что качать нечего. Это обычный исход
    /// планового запуска скрипта, и он должен быть дешёвым.
    /// </summary>
    [Fact]
    public async Task BuildPlanAsync_ManifestsMatch_NothingToDownload()
    {
        await AddServerFileAsync("a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111");

        var plan = await _service.BuildPlanAsync(CreateRequest(new()
        {
            ["a.txt"] = "aaaa1111aaaa1111aaaa1111aaaa1111"
        }));

        Assert.Equal(UpdateStatus.Ok, plan.Status);
        Assert.Empty(plan.FilesToDownload);
        Assert.Empty(plan.ExtraFiles);
        Assert.Equal(0, plan.TotalSizeBytes);
    }

    /// <summary>Отсутствующий у клиента файл попадает в план.</summary>
    [Fact]
    public async Task BuildPlanAsync_FileMissingOnClient_IncludedInPlan()
    {
        await AddServerFileAsync("a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111", size: 512);

        var plan = await _service.BuildPlanAsync(CreateRequest([]));

        Assert.Equal(UpdateStatus.Update, plan.Status);
        var file = Assert.Single(plan.FilesToDownload);
        Assert.Equal("a.txt", file.RelativePath);
        Assert.Null(file.ClientMd5Hash);
        Assert.Equal(512, plan.TotalSizeBytes);
    }

    /// <summary>Различие сумм означает изменённый файл; прежняя сумма сохраняется для журнала.</summary>
    [Fact]
    public async Task BuildPlanAsync_HashesDiffer_IncludedWithPreviousHash()
    {
        await AddServerFileAsync("a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111");

        var plan = await _service.BuildPlanAsync(CreateRequest(new()
        {
            ["a.txt"] = "bbbb2222bbbb2222bbbb2222bbbb2222"
        }));

        var file = Assert.Single(plan.FilesToDownload);
        Assert.Equal("bbbb2222bbbb2222bbbb2222bbbb2222", file.ClientMd5Hash);
        Assert.Equal("aaaa1111aaaa1111aaaa1111aaaa1111", file.Md5Hash);
    }

    /// <summary>
    /// Регистр присланной суммы не влияет на сравнение: часть утилит выдаёт её
    /// заглавными, и считать такой файл изменившимся было бы ошибкой ценой
    /// в лишнюю передачу.
    /// </summary>
    [Fact]
    public async Task BuildPlanAsync_UpperCaseClientHash_TreatedAsMatch()
    {
        await AddServerFileAsync("a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111");

        var plan = await _service.BuildPlanAsync(CreateRequest(new()
        {
            ["a.txt"] = "AAAA1111AAAA1111AAAA1111AAAA1111"
        }));

        Assert.Empty(plan.FilesToDownload);
    }

    /// <summary>
    /// Файлы, которых нет на сервере, только отмечаются и никогда не попадают
    /// в список к скачиванию. Удалять их клиент не должен: достаточно случайно
    /// отмонтировать каталог раздачи, чтобы приказать всем стереть свои данные.
    /// </summary>
    [Fact]
    public async Task BuildPlanAsync_ExtraFileOnClient_OnlyReported()
    {
        await AddServerFileAsync("a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111");

        var plan = await _service.BuildPlanAsync(CreateRequest(new()
        {
            ["a.txt"] = "aaaa1111aaaa1111aaaa1111aaaa1111",
            ["старый.txt"] = "cccc3333cccc3333cccc3333cccc3333"
        }));

        Assert.Equal(UpdateStatus.Ok, plan.Status);
        Assert.Empty(plan.FilesToDownload);
        Assert.Equal(["старый.txt"], plan.ExtraFiles);
    }

    /// <summary>Пустой манифест сервера означает пустой план, а не команду на удаление.</summary>
    [Fact]
    public async Task BuildPlanAsync_EmptyServerManifest_NothingToDownload()
    {
        var plan = await _service.BuildPlanAsync(CreateRequest(new()
        {
            ["a.txt"] = "aaaa1111aaaa1111aaaa1111aaaa1111"
        }));

        Assert.Equal(UpdateStatus.Ok, plan.Status);
        Assert.Empty(plan.FilesToDownload);
        Assert.Single(plan.ExtraFiles);
    }

    /// <summary>
    /// Суммарный объём считается по файлам к скачиванию — по нему клиент
    /// проверяет, хватит ли места на диске.
    /// </summary>
    [Fact]
    public async Task BuildPlanAsync_TotalSize_CountsOnlyFilesToDownload()
    {
        await AddServerFileAsync("a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111", size: 1000);
        await AddServerFileAsync("b.iso", "bbbb2222bbbb2222bbbb2222bbbb2222", size: 6_000_000_000);
        await AddServerFileAsync("c.txt", "cccc3333cccc3333cccc3333cccc3333", size: 50);

        var plan = await _service.BuildPlanAsync(CreateRequest(new()
        {
            ["c.txt"] = "cccc3333cccc3333cccc3333cccc3333"
        }));

        Assert.Equal(2, plan.FilesToDownload.Count);
        Assert.Equal(6_000_001_000, plan.TotalSizeBytes);
    }

    /// <summary>
    /// Файлы упорядочены по пути: план должен быть воспроизводимым,
    /// иначе один и тот же запрос давал бы разный ответ.
    /// </summary>
    [Fact]
    public async Task BuildPlanAsync_FilesOrderedByPath()
    {
        await AddServerFileAsync("я.txt", "aaaa1111aaaa1111aaaa1111aaaa1111");
        await AddServerFileAsync("a.txt", "bbbb2222bbbb2222bbbb2222bbbb2222");
        await AddServerFileAsync("m.txt", "cccc3333cccc3333cccc3333cccc3333");

        var plan = await _service.BuildPlanAsync(CreateRequest([]));

        var paths = plan.FilesToDownload.Select(f => f.RelativePath).ToList();
        Assert.Equal(paths.OrderBy(p => p, StringComparer.Ordinal), paths);
    }

    /// <summary>В плане передаётся текущее поколение манифеста.</summary>
    [Fact]
    public async Task BuildPlanAsync_PlanCarriesManifestGeneration()
    {
        _state.CompleteScan(entryCount: 1, totalSizeBytes: 1, rejectedPaths: [], hasChanges: true);

        var plan = await _service.BuildPlanAsync(CreateRequest([]));

        Assert.Equal(_state.Generation, plan.Generation);
    }

    /// <summary>Освобождает базу.</summary>
    public void Dispose()
    {
        _database.Dispose();
        GC.SuppressFinalize(this);
    }
}
