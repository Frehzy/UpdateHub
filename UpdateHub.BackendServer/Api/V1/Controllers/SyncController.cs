using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using UpdateHub.BackendServer.Application.Abstractions.Services.Clients;
using UpdateHub.BackendServer.Application.Abstractions.Services.Manifest;
using UpdateHub.BackendServer.Application.Abstractions.Services.Updates;
using UpdateHub.BackendServer.Application.Manifest;
using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.BackendServer.Infrastructure.Configuration;

namespace UpdateHub.BackendServer.Api.V1.Controllers;

/// <summary>
/// Сравнение манифеста клиента с эталонным.
/// </summary>
/// <param name="syncService">Сравнение манифестов.</param>
/// <param name="manifestService">Чтение эталонного манифеста.</param>
/// <param name="clientAccessService">Проверка прав на компьютер.</param>
/// <param name="statisticsService">Журналирование обращений.</param>
/// <param name="config">Настройки раздачи.</param>
/// <param name="logger">Журнал.</param>
[ApiController]
[Route("api/v1/sync")]
[Authorize]
[Produces("text/plain")]
public class SyncController(
    ISyncService syncService,
    IManifestService manifestService,
    IClientAccessService clientAccessService,
    IStatisticsService statisticsService,
    IOptions<UpdateHubConfig> config,
    ILogger<SyncController> logger) : ApiControllerBase
{
    private readonly UpdateHubConfig _config = config.Value;

    /// <summary>
    /// Сравнивает присланный манифест с эталонным и возвращает план работ.
    /// </summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="check">
    /// Только сверка, без намерения скачивать. Влияет на тип записи в журнале
    /// и на то, сохраняется ли пофайловая детализация.
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>План синхронизации в текстовом виде.</returns>
    /// <response code="200">План составлен.</response>
    /// <response code="400">Манифест не удалось разобрать.</response>
    /// <response code="403">Нет прав на компьютер либо он заблокирован.</response>
    /// <response code="404">Компьютер не зарегистрирован.</response>
    /// <remarks>
    /// <para>
    /// Тело запроса — вывод команды <c>md5sum</c> по каталогу клиента:
    /// строки вида «сумма, два пробела, путь». Ответ устроен так, чтобы
    /// строки к скачиванию сами были манифестом:
    /// </para>
    /// <code>
    /// @GENERATION 42
    /// @COUNT 3
    /// @SIZE 15728640
    /// !docs/старый-файл.txt
    /// d41d8cd98f00b204e9800998ecf8427e  bin/app
    /// </code>
    /// <para>
    /// Строки с «@» — сведения о плане, с «!» — файлы, которых нет на сервере
    /// (клиент их не удаляет, только показывает пользователю), остальные —
    /// файлы к скачиванию. Отфильтровав их, клиент получает готовый файл
    /// для <c>md5sum -c</c> и проверяет закачку без дополнительного кода.
    /// </para>
    /// </remarks>
    [HttpPost("diff")]
    [Consumes("text/plain")]
    public async Task<IActionResult> Diff(
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "check")] bool check = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return TextError(StatusCodes.Status400BadRequest, "Не указан параметр client_id");
        }

        var access = await clientAccessService.AuthorizeAsync(CurrentUserId, IsAdmin, clientId, cancellationToken);
        if (!access.IsAllowed)
        {
            var status = access.Outcome == ClientAccessOutcome.UnknownClient
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status403Forbidden;

            return TextError(status, access.Reason!);
        }

        var stopwatch = Stopwatch.StartNew();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken);

        var parsed = ManifestFormat.Parse(body, _config.MaxClientManifestEntries);

        // Разбор не прерывается на первой плохой строке: клиенту полезнее
        // получить весь список замечаний разом.
        if (parsed.Errors.Count > 0)
        {
            logger.LogWarning(
                "Манифест компьютера {ClientId} содержит {Count} ошибок: {Errors}",
                clientId, parsed.Errors.Count, string.Join("; ", parsed.Errors.Take(5)));
        }

        var request = new SyncRequest(
            clientId,
            CurrentUsername,
            check ? RequestType.Check : RequestType.Sync,
            parsed.Entries);

        var plan = await syncService.BuildPlanAsync(request, cancellationToken);

        stopwatch.Stop();
        await statisticsService.LogSyncAsync(plan, request, (int)stopwatch.ElapsedMilliseconds, cancellationToken);

        return Content(RenderPlan(plan, parsed.Errors), "text/plain; charset=utf-8");
    }

    /// <summary>
    /// Возвращает эталонный манифест целиком в формате <c>md5sum</c>.
    /// </summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Текст манифеста.</returns>
    /// <response code="200">Манифест возвращён.</response>
    /// <response code="403">Нет прав на компьютер либо он заблокирован.</response>
    /// <response code="404">Компьютер не зарегистрирован.</response>
    /// <remarks>
    /// Пригодится после начальной заливки с флешки: командой
    /// <c>md5sum -c manifest.txt</c> можно убедиться, что скопированное
    /// совпадает с эталоном, не обращаясь к сравнению на сервере.
    /// </remarks>
    [HttpGet("manifest")]
    public async Task<IActionResult> GetManifest(
        [FromQuery(Name = "client_id")] string clientId,
        CancellationToken cancellationToken)
    {
        var access = await clientAccessService.AuthorizeAsync(CurrentUserId, IsAdmin, clientId, cancellationToken);
        if (!access.IsAllowed)
        {
            var status = access.Outcome == ClientAccessOutcome.UnknownClient
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status403Forbidden;

            return TextError(status, access.Reason!);
        }

        var manifest = await manifestService.RenderManifestAsync(cancellationToken);
        return Content(manifest, "text/plain; charset=utf-8");
    }

    /// <summary>
    /// Собирает текстовое представление плана синхронизации.
    /// </summary>
    /// <param name="plan">План синхронизации.</param>
    /// <param name="parseErrors">Замечания по разбору манифеста клиента.</param>
    /// <returns>Текст ответа.</returns>
    private static string RenderPlan(SyncPlan plan, IReadOnlyList<string> parseErrors)
    {
        var builder = new StringBuilder();

        builder.Append("@GENERATION ").Append(plan.Generation).Append('\n');
        builder.Append("@STATUS ").Append(plan.Status == UpdateStatus.Update ? "update" : "ok").Append('\n');
        builder.Append("@COUNT ").Append(plan.FilesToDownload.Count).Append('\n');
        builder.Append("@SIZE ").Append(plan.TotalSizeBytes).Append('\n');

        foreach (var error in parseErrors)
        {
            builder.Append("@WARN ").Append(error).Append('\n');
        }

        foreach (var extra in plan.ExtraFiles)
        {
            builder.Append('!').Append(extra).Append('\n');
        }

        foreach (var file in plan.FilesToDownload)
        {
            ManifestFormat.AppendLine(builder, file.Md5Hash, file.RelativePath);
        }

        return builder.ToString();
    }
}
