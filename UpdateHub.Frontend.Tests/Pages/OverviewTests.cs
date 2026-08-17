using Bunit;
using Microsoft.Extensions.DependencyInjection;
using UpdateHub.Frontend.Tests.TestSupport;
using UpdateHub.FrontendServer.Pages;
using UpdateHub.FrontendServer.Services;
using UpdateHub.Shared.Contracts.Clients;
using UpdateHub.Shared.Contracts.Maintenance;
using UpdateHub.Shared.Contracts.Manifest;
using UpdateHub.Shared.Contracts.Statistics;

namespace UpdateHub.Frontend.Tests.Pages;

/// <summary>
/// Проверяет разметку страницы обзора.
/// </summary>
/// <remarks>
/// Первые проверки разметки в этом проекте. Прежде панель проверялась только
/// со стороны служб — разбор ответов, хранение токенов, — а сама разметка
/// не проверялась ничем. Между тем администратор видит систему только через
/// неё, и обращение к пустой ссылке в шаблоне даёт пустую страницу вместо
/// сведений, без всякой ошибки в журнале.
/// <para>
/// Проверяется прежде всего раздел резервного копирования: в нём больше всего
/// условий — копий нет, последняя попытка не удалась, копирование отключено, —
/// и каждое из них показывает администратору то, ради чего раздел и заведён.
/// </para>
/// <para>
/// Базовый класс называется <c>BunitContext</c>, а не <c>TestContext</c>:
/// в xunit.v3 есть свой <c>TestContext</c>, и имена столкнулись бы. По той же
/// причине его переименовали и в самом bUnit.
/// </para>
/// </remarks>
public class OverviewTests : BunitContext
{
    /// <summary>
    /// Готовит окружение страницы с заданным состоянием обслуживания.
    /// </summary>
    /// <param name="maintenance">Состояние обслуживания, которое отдаст сервер.</param>
    /// <returns>Отрисованная страница.</returns>
    /// <remarks>
    /// Страница запрашивает четыре адреса, и ответ нужен на все: неотвеченный
    /// превратился бы в сообщение об ошибке поверх проверяемого раздела.
    /// </remarks>
    private IRenderedComponent<Overview> RenderOverview(MaintenanceStatusDto maintenance)
    {
        var handler = new StubHttpHandler()
            .Respond("api/v1/admin/manifest/status", new ManifestStatusResponseDto())
            .Respond("api/v1/admin/stats", new StatsResponseDto())
            .Respond("api/v1/admin/clients/stale", new StaleClientListResponseDto())
            .Respond("api/v1/admin/maintenance", maintenance);

        var http = new HttpClient(handler) { BaseAddress = new Uri("http://updatehub-test/") };

        // Обращения к localStorage идут через JS. В проверках его нет, поэтому
        // вызовы разрешаются свободно и возвращают пустоту: токен не нужен,
        // подставной обработчик отвечает независимо от заголовков.
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton(http);
        Services.AddSingleton<AuthState>();
        Services.AddSingleton<ApiClient>();

        return Render<Overview>();
    }

    /// <summary>Удачная копия показывается с временем и размером.</summary>
    [Fact]
    public void Overview_ShowsLastSuccessfulBackup()
    {
        var page = RenderOverview(new MaintenanceStatusDto
        {
            LastSuccessAt = new DateTime(2026, 8, 17, 3, 0, 0, DateTimeKind.Utc),
            LastSuccessSizeBytes = 2_516_582,
            LastAttemptAt = new DateTime(2026, 8, 17, 3, 0, 0, DateTimeKind.Utc),
            LastAttemptSucceeded = true,
            BackupFilesOnDisk = 7,
            BackupPath = "/app/backup",
            IntervalHours = 24,
            KeepCount = 7,
            BackupFreeBytes = 50_000_000_000,
            BackupTotalBytes = 100_000_000_000
        });

        var markup = page.Markup;

        Assert.Contains("Резервная копия базы", markup);
        Assert.Contains("17.08.2026", markup);
        Assert.Contains("каждые 24 ч, хранить 7", markup);
        Assert.Contains("/app/backup", markup);

        // Тревожной пометки быть не должно: всё в порядке.
        Assert.DoesNotContain("с момента запуска ни одной", markup);
    }

    /// <summary>
    /// Отсутствие удачных копий показывается тревожной пометкой.
    /// </summary>
    /// <remarks>
    /// Худший случай из возможных: копирование не работает, и узнать об этом
    /// иначе как заглянув в папку на сервере было нельзя.
    /// </remarks>
    [Fact]
    public void Overview_WithoutSuccessfulBackup_ShowsWarning()
    {
        var page = RenderOverview(new MaintenanceStatusDto
        {
            LastSuccessAt = null,
            BackupPath = "/app/backup",
            IntervalHours = 24,
            KeepCount = 7
        });

        Assert.Contains("с момента запуска ни одной", page.Markup);
        Assert.Contains("trevoga", page.Markup);
    }

    /// <summary>Неудачная последняя попытка показывается вместе с причиной.</summary>
    [Fact]
    public void Overview_FailedAttempt_ShowsReason()
    {
        var page = RenderOverview(new MaintenanceStatusDto
        {
            LastSuccessAt = new DateTime(2026, 8, 10, 3, 0, 0, DateTimeKind.Utc),
            LastSuccessSizeBytes = 2_516_582,
            LastAttemptAt = new DateTime(2026, 8, 17, 3, 0, 0, DateTimeKind.Utc),
            LastAttemptSucceeded = false,
            LastAttemptError = "нет места на диске",
            BackupPath = "/app/backup",
            IntervalHours = 24,
            KeepCount = 7
        });

        var markup = page.Markup;

        Assert.Contains("не удалась", markup);
        Assert.Contains("нет места на диске", markup);

        // Копия недельной давности при этом остаётся видна: администратору
        // важно знать, к какому состоянию он может вернуться.
        Assert.Contains("10.08.2026", markup);
    }

    /// <summary>Отключённое копирование показывается словами, а не пустым расписанием.</summary>
    [Fact]
    public void Overview_DisabledBackup_SaysSo()
    {
        var page = RenderOverview(new MaintenanceStatusDto
        {
            BackupPath = "/app/backup",
            IntervalHours = 0,
            KeepCount = 7
        });

        Assert.Contains("копирование отключено настройкой", page.Markup);
    }

    /// <summary>Неизвестное свободное место показывается словом, а не нулём.</summary>
    /// <remarks>
    /// Раздел диска определить не всегда удаётся. Ноль на этом месте читался бы
    /// как «места нет» и заставил бы разбираться там, где разбираться нечего.
    /// </remarks>
    [Fact]
    public void Overview_UnknownDiskSpace_ShownAsWord()
    {
        var page = RenderOverview(new MaintenanceStatusDto
        {
            BackupPath = "/app/backup",
            IntervalHours = 24,
            KeepCount = 7,
            BackupFreeBytes = null,
            BackupTotalBytes = null,
            FilesFreeBytes = null,
            FilesTotalBytes = null
        });

        Assert.Contains("неизвестно", page.Markup);
    }
}
