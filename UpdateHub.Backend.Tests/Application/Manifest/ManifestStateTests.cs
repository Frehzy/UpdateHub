using UpdateHub.BackendServer.Application.Manifest;

namespace UpdateHub.Backend.Tests.Application.Manifest;

/// <summary>
/// Проверяет общее состояние манифеста: взаимное исключение обходов
/// и учёт поколений.
/// </summary>
/// <remarks>
/// Этот тип появился как исправление конкретного дефекта: раньше семафор
/// и признак «идёт пересборка» лежали в службе с областью жизни запроса,
/// поэтому у каждого запроса был свой семафор, взаимного исключения
/// не возникало вовсе, а признак всегда читался как ложь.
/// </remarks>
public class ManifestStateTests
{
    /// <summary>Первый вызов получает право на обход.</summary>
    [Fact]
    public async Task TryBeginScanAsync_FirstCall_GrantsScanRight()
    {
        var state = new ManifestState();

        using var scope = await state.TryBeginScanAsync();

        Assert.NotNull(scope);
        Assert.True(state.IsScanning);
    }

    /// <summary>
    /// Второй вызов во время удержания права получает отказ. Именно на этом
    /// держится защита от одновременного обхода фоновой службой и запросом
    /// администратора: два обхода писали бы в таблицу с уникальным индексом.
    /// </summary>
    [Fact]
    public async Task TryBeginScanAsync_WhileRightIsHeld_SecondCallDenied()
    {
        var state = new ManifestState();

        using var first = await state.TryBeginScanAsync();
        var second = await state.TryBeginScanAsync();

        Assert.NotNull(first);
        Assert.Null(second);
    }

    /// <summary>После освобождения право выдаётся снова.</summary>
    [Fact]
    public async Task TryBeginScanAsync_AfterRelease_GrantsRightAgain()
    {
        var state = new ManifestState();

        var first = await state.TryBeginScanAsync();
        first!.Dispose();

        using var second = await state.TryBeginScanAsync();

        Assert.NotNull(second);
        Assert.True(state.IsScanning);
    }

    /// <summary>После освобождения признак обхода снимается.</summary>
    [Fact]
    public async Task Dispose_ClearsScanningFlag()
    {
        var state = new ManifestState();

        var scope = await state.TryBeginScanAsync();
        Assert.True(state.IsScanning);

        scope!.Dispose();

        Assert.False(state.IsScanning);
    }

    /// <summary>
    /// Поколение растёт только когда обход действительно нашёл изменения.
    /// Иначе номер увеличивался бы каждые несколько десятков секунд
    /// и перестал бы что-либо означать.
    /// </summary>
    [Fact]
    public void CompleteScan_WithoutChanges_GenerationUnchanged()
    {
        var state = new ManifestState();
        var before = state.Generation;

        state.CompleteScan(entryCount: 5, totalSizeBytes: 100, rejectedPaths: [], hasChanges: false);

        Assert.Equal(before, state.Generation);
    }

    /// <summary>При обнаруженных изменениях поколение увеличивается на единицу.</summary>
    [Fact]
    public void CompleteScan_WithChanges_GenerationIncremented()
    {
        var state = new ManifestState();
        var before = state.Generation;

        state.CompleteScan(entryCount: 5, totalSizeBytes: 100, rejectedPaths: [], hasChanges: true);

        Assert.Equal(before + 1, state.Generation);
    }

    /// <summary>Итоги обхода сохраняются и доступны для панели управления.</summary>
    [Fact]
    public void CompleteScan_StoresScanResults()
    {
        var state = new ManifestState();
        string[] rejected = ["Doc.txt: конфликт регистра"];

        state.CompleteScan(entryCount: 70, totalSizeBytes: 7_516_192_768, rejectedPaths: rejected, hasChanges: true);

        Assert.Equal(70, state.EntryCount);
        Assert.Equal(7_516_192_768, state.TotalSizeBytes);
        Assert.Equal(rejected, state.RejectedPaths);
        Assert.NotNull(state.LastScanCompletedAt);
    }

    /// <summary>
    /// До первого обхода отметка о завершении отсутствует. По ней сводка
    /// при старте отличает «каталог пуст» от «обход ещё идёт».
    /// </summary>
    [Fact]
    public void BeforeFirstScan_CompletionTimestampIsNull()
    {
        var state = new ManifestState();

        Assert.Null(state.LastScanCompletedAt);
        Assert.Equal(0, state.Generation);
        Assert.False(state.IsScanning);
    }

    /// <summary>
    /// При одновременном обращении право получает ровно один вызывающий.
    /// Проверка нужна потому, что за право борются фоновая служба и запросы
    /// администратора, приходящие в произвольный момент.
    /// </summary>
    [Fact]
    public async Task TryBeginScanAsync_ConcurrentCalls_OnlyOneSucceeds()
    {
        var state = new ManifestState();

        var attempts = await Task.WhenAll(
            Enumerable.Range(0, 20).Select(_ => Task.Run(async () => await state.TryBeginScanAsync())));

        var granted = attempts.Where(s => s is not null).ToList();

        Assert.Single(granted);

        foreach (var scope in granted)
        {
            scope!.Dispose();
        }
    }
}
