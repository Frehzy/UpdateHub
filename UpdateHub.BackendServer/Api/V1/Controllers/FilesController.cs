using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using UpdateHub.BackendServer.Application.Abstractions.Services;
using UpdateHub.BackendServer.Application.Sync;

namespace UpdateHub.BackendServer.Api.V1.Controllers;

/// <summary>
/// Раздача файлов обновлений.
/// </summary>
/// <param name="manifestService">Чтение эталонного манифеста.</param>
/// <param name="clientAccessService">Проверка прав на компьютер.</param>
/// <param name="logger">Журнал.</param>
[ApiController]
[Route("api/v1/files")]
[Authorize]
public class FilesController(
    IManifestService manifestService,
    IClientAccessService clientAccessService,
    ILogger<FilesController> logger) : ApiControllerBase
{
    /// <summary>
    /// Отдаёт файл по его пути в манифесте.
    /// </summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="path">Путь файла относительно каталога раздачи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Содержимое файла.</returns>
    /// <response code="200">Файл отдан целиком.</response>
    /// <response code="206">Отдана запрошенная часть файла.</response>
    /// <response code="403">Нет прав на компьютер либо он заблокирован.</response>
    /// <response code="404">Компьютер не зарегистрирован либо файла нет в манифесте.</response>
    /// <response code="412">Файл изменился с момента начала закачки.</response>
    /// <remarks>
    /// <para>
    /// Файл адресуется путём, а не идентификатором: клиент уже знает путь
    /// из ответа на сравнение манифестов, и лишний столбец в протоколе не нужен.
    /// Выход за пределы каталога при этом невозможен — путь ищется точным
    /// совпадением в манифесте, куда попадает только результат обхода каталога.
    /// </para>
    /// <para>
    /// Докачка включена: <c>curl -C -</c> продолжает с места обрыва. Значением
    /// ETag служит контрольная сумма файла, поэтому подмена файла на сервере
    /// во время закачки приведёт к ответу 412, а не к склейке двух разных версий.
    /// Это существенно на шестигигабайтном образе, который по каналу 2 Мбит/с
    /// передаётся почти семь часов.
    /// </para>
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Download(
        [FromQuery(Name = "client_id")] string clientId,
        [FromQuery(Name = "path")] string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(path))
        {
            return TextError(StatusCodes.Status400BadRequest, "Не указаны параметры client_id и path");
        }

        var access = await clientAccessService.AuthorizeAsync(CurrentUserId, IsAdmin, clientId, cancellationToken);
        if (!access.IsAllowed)
        {
            var status = access.Outcome == ClientAccessOutcome.UnknownClient
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status403Forbidden;

            return TextError(status, access.Reason!);
        }

        var resolved = await manifestService.ResolveFileAsync(path, cancellationToken);
        if (resolved is null)
        {
            logger.LogWarning("Компьютер {ClientId} запросил отсутствующий файл {Path}", clientId, path);
            return TextError(StatusCodes.Status404NotFound, $"Файл '{path}' не найден");
        }

        var (entry, fullPath) = resolved.Value;

        // PhysicalFile отдаёт файл средствами операционной системы и не держит
        // открытый поток при обрыве соединения.
        return PhysicalFile(
            fullPath,
            "application/octet-stream",
            Path.GetFileName(entry.RelativePath),
            new DateTimeOffset(entry.LastModified, TimeSpan.Zero),
            new EntityTagHeaderValue($"\"{entry.Md5Hash}\""),
            enableRangeProcessing: true);
    }
}
