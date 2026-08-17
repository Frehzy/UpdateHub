using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UpdateHub.Backend.Tests.TestSupport;
using UpdateHub.BackendServer.Application.Repositories.Clients;
using UpdateHub.BackendServer.Application.Repositories.Updates;
using UpdateHub.BackendServer.Application.Services.Updates;
using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Domain.Entities.Clients;
using UpdateHub.BackendServer.Domain.Entities.Manifest;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.BackendServer.Infrastructure.Configuration;

namespace UpdateHub.Backend.Tests.Application.Services.Updates;

/// <summary>
/// Проверяет журналирование обращений и сводную статистику.
/// </summary>
/// <remarks>
/// Ключевая проверка здесь — что одно обращение порождает ровно одну запись.
/// Раньше её создавали и сервис сравнения манифестов, и сервис статистики,
/// из-за чего все цифры в панели управления были завышены ровно вдвое,
/// а число уникальных компьютеров оставалось верным — расхождение
/// было почти незаметным.
/// </remarks>
public class StatisticsServiceTests : IDisposable
{
    private readonly TestDatabase _database;
    private readonly StatisticsService _service;

    /// <summary>Готовит базу и службу.</summary>
    public StatisticsServiceTests()
    {
        _database = new TestDatabase();
        _service = new StatisticsService(
            new UpdateRequestRepository(_database.Context),
            new UpdateDetailRepository(_database.Context),
            new ClientRepository(_database.Context),
            Options.Create(new UpdateHubConfig()),
            NullLogger<StatisticsService>.Instance);
    }

    /// <summary>Заводит компьютер, на который ссылается журнал обращений.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    private async Task AddClientAsync(string clientId)
    {
        _database.Context.Clients.Add(new ClientEntity { Id = clientId, IsActive = true });
        await _database.Context.SaveChangesAsync();
    }

    /// <summary>
    /// Заводит запись эталонного манифеста и возвращает её идентификатор.
    /// </summary>
    /// <param name="path">Путь файла.</param>
    /// <param name="md5">Контрольная сумма.</param>
    /// <returns>Идентификатор записи манифеста.</returns>
    /// <remarks>
    /// Пофайловая детализация ссылается на запись манифеста внешним ключом,
    /// поэтому выдуманный идентификатор в неё вставить нельзя.
    /// </remarks>
    private async Task<string> AddManifestEntryAsync(string path, string md5)
    {
        var entry = new ManifestEntryEntity
        {
            RelativePath = path,
            Md5Hash = md5,
            SizeBytes = 100,
            LastModified = DateTime.UtcNow
        };

        _database.Context.ManifestEntries.Add(entry);
        await _database.Context.SaveChangesAsync();

        return entry.Id;
    }

    /// <summary>Собирает запрос на сравнение.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="type">Тип обращения.</param>
    /// <returns>Запрос.</returns>
    private static SyncRequest CreateRequest(string clientId = "pc-1", RequestType type = RequestType.Sync)
        => new(clientId, "ivanov", type, new Dictionary<string, string>());

    /// <summary>Собирает план синхронизации с заданными файлами.</summary>
    /// <param name="files">Файлы к скачиванию.</param>
    /// <returns>План.</returns>
    private static SyncPlan CreatePlan(params SyncFile[] files)
        => new(
            files.Length > 0 ? UpdateStatus.Update : UpdateStatus.Ok,
            Generation: 1,
            files,
            ExtraFiles: []);

    /// <summary>Одно обращение создаёт ровно одну запись в журнале.</summary>
    [Fact]
    public async Task LogSyncAsync_SingleRequest_CreatesOneRecord()
    {
        await AddClientAsync("pc-1");

        await _service.LogSyncAsync(CreatePlan(), CreateRequest(), responseTimeMs: 12);

        using var context = _database.CreateSeparateContext();
        Assert.Single(context.UpdateRequests);
    }

    /// <summary>В записи сохраняются компьютер, пользователь, тип и время ответа.</summary>
    [Fact]
    public async Task LogSyncAsync_StoresRequestDetails()
    {
        await AddClientAsync("pc-1");

        await _service.LogSyncAsync(CreatePlan(), CreateRequest(type: RequestType.Check), responseTimeMs: 42);

        using var context = _database.CreateSeparateContext();
        var record = context.UpdateRequests.Single();

        Assert.Equal("pc-1", record.ClientId);
        Assert.Equal("ivanov", record.Username);
        Assert.Equal(RequestType.Check, record.RequestType);
        Assert.Equal(42, record.ResponseTimeMs);
    }

    /// <summary>Число файлов и суммарный объём берутся из плана.</summary>
    [Fact]
    public async Task LogSyncAsync_StoresPayloadSize()
    {
        await AddClientAsync("pc-1");
        var first = await AddManifestEntryAsync("a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111");
        var second = await AddManifestEntryAsync("b.iso", "bbbb2222bbbb2222bbbb2222bbbb2222");

        var plan = CreatePlan(
            new SyncFile(first, "a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111", 1000, null),
            new SyncFile(second, "b.iso", "bbbb2222bbbb2222bbbb2222bbbb2222", 6_000_000_000, null));

        await _service.LogSyncAsync(plan, CreateRequest(), responseTimeMs: 5);

        using var context = _database.CreateSeparateContext();
        var record = context.UpdateRequests.Single();

        Assert.Equal(2, record.FilesToUpdate);
        Assert.Equal(6_000_001_000, record.TotalSizeBytes);
        Assert.Equal(UpdateStatus.Update, record.Status);
    }

    /// <summary>Пофайловая детализация сохраняется при подготовке к скачиванию.</summary>
    [Fact]
    public async Task LogSyncAsync_SyncType_StoresPerFileDetails()
    {
        await AddClientAsync("pc-1");
        var entryId = await AddManifestEntryAsync("a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111");

        var plan = CreatePlan(
            new SyncFile(entryId, "a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111", 100, "старая-сумма"));

        await _service.LogSyncAsync(plan, CreateRequest(type: RequestType.Sync), responseTimeMs: 5);

        using var context = _database.CreateSeparateContext();
        var detail = context.UpdateDetails.Single();

        Assert.Equal("a.txt", detail.RelativePath);
        Assert.Equal("старая-сумма", detail.OldMd5Hash);
        Assert.Equal("aaaa1111aaaa1111aaaa1111aaaa1111", detail.NewMd5Hash);
    }

    /// <summary>
    /// Для простой сверки детализация не пишется: клиент ничего качать
    /// не собирался, и заполнять ею таблицу незачем.
    /// </summary>
    [Fact]
    public async Task LogSyncAsync_CheckType_SkipsPerFileDetails()
    {
        await AddClientAsync("pc-1");
        var entryId = await AddManifestEntryAsync("a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111");

        var plan = CreatePlan(new SyncFile(entryId, "a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111", 100, null));

        await _service.LogSyncAsync(plan, CreateRequest(type: RequestType.Check), responseTimeMs: 5);

        using var context = _database.CreateSeparateContext();
        Assert.Empty(context.UpdateDetails);
    }

    /// <summary>Отпечаток манифеста клиента сохраняется для сопоставления обращений.</summary>
    [Fact]
    public async Task LogSyncAsync_StoresClientManifestFingerprint()
    {
        await AddClientAsync("pc-1");
        var request = new SyncRequest("pc-1", "ivanov", RequestType.Sync, new Dictionary<string, string>
        {
            ["a.txt"] = "aaaa1111aaaa1111aaaa1111aaaa1111"
        });

        await _service.LogSyncAsync(CreatePlan(), request, responseTimeMs: 5);

        using var context = _database.CreateSeparateContext();
        Assert.False(string.IsNullOrEmpty(context.UpdateRequests.Single().ClientManifestHash));
    }

    /// <summary>Для пустого манифеста отпечаток пуст — считать нечего.</summary>
    [Fact]
    public async Task LogSyncAsync_EmptyManifest_FingerprintIsEmpty()
    {
        await AddClientAsync("pc-1");

        await _service.LogSyncAsync(CreatePlan(), CreateRequest(), responseTimeMs: 5);

        using var context = _database.CreateSeparateContext();
        Assert.Equal(string.Empty, context.UpdateRequests.Single().ClientManifestHash);
    }

    /// <summary>Сводка считает обращения, различные компьютеры и суммарный объём.</summary>
    [Fact]
    public async Task GetStatisticsAsync_CountsRequestsAndClients()
    {
        await AddClientAsync("pc-1");
        await AddClientAsync("pc-2");
        var entryId = await AddManifestEntryAsync("a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111");

        var plan = CreatePlan(new SyncFile(entryId, "a.txt", "aaaa1111aaaa1111aaaa1111aaaa1111", 500, null));

        await _service.LogSyncAsync(plan, CreateRequest("pc-1"), responseTimeMs: 1);
        await _service.LogSyncAsync(plan, CreateRequest("pc-1"), responseTimeMs: 1);
        await _service.LogSyncAsync(plan, CreateRequest("pc-2"), responseTimeMs: 1);

        var stats = await _service.GetStatisticsAsync(days: null);

        Assert.Equal(3, stats.TotalRequests);
        Assert.Equal(2, stats.UniqueClients);
        Assert.Equal(1500, stats.TotalDownloadedBytes);
    }

    /// <summary>На пустой базе сводка возвращает нули, а не падает.</summary>
    [Fact]
    public async Task GetStatisticsAsync_EmptyDatabase_ReturnsZeros()
    {
        var stats = await _service.GetStatisticsAsync(days: null);

        Assert.Equal(0, stats.TotalRequests);
        Assert.Equal(0, stats.UniqueClients);
        Assert.Equal(0, stats.TotalDownloadedBytes);
        Assert.Empty(stats.RequestsByDay);
    }

    /// <summary>Ограничение по периоду отсекает старые обращения.</summary>
    [Fact]
    public async Task GetStatisticsAsync_PeriodFilter_ExcludesOldRequests()
    {
        await AddClientAsync("pc-1");
        await _service.LogSyncAsync(CreatePlan(), CreateRequest(), responseTimeMs: 1);

        // Сдвигаем единственную запись на сорок суток назад.
        var record = _database.Context.UpdateRequests.Single();
        record.RequestTimestamp = DateTime.UtcNow.AddDays(-40);
        await _database.Context.SaveChangesAsync();

        var recent = await _service.GetStatisticsAsync(days: 30);
        var all = await _service.GetStatisticsAsync(days: null);

        Assert.Equal(0, recent.TotalRequests);
        Assert.Equal(1, all.TotalRequests);
    }

    /// <summary>Разбивка по дням группирует обращения по дате.</summary>
    [Fact]
    public async Task GetStatisticsAsync_DailyBreakdown_GroupsByDate()
    {
        await AddClientAsync("pc-1");
        await _service.LogSyncAsync(CreatePlan(), CreateRequest(), responseTimeMs: 1);
        await _service.LogSyncAsync(CreatePlan(), CreateRequest(), responseTimeMs: 1);

        var stats = await _service.GetStatisticsAsync(days: null);

        var day = Assert.Single(stats.RequestsByDay);
        Assert.Equal(2, day.Count);
    }

    /// <summary>Освобождает базу.</summary>
    public void Dispose()
    {
        _database.Dispose();
        GC.SuppressFinalize(this);
    }
}
