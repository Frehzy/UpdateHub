using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UpdateHub.BackendServer.Api.V1.DTOs.Request;
using UpdateHub.BackendServer.Application.Abstractions.Services.Enrollments;

namespace UpdateHub.BackendServer.Api.V1.Controllers;

/// <summary>
/// Приём заявок на регистрацию компьютеров.
/// </summary>
/// <param name="enrollmentService">Служба заявок.</param>
/// <remarks>
/// Единственный эндпоинт клиентской части, доступный без авторизации.
/// Он нужен, чтобы пользователь незарегистрированного компьютера не оказался
/// в тупике: сервер таких компьютеров сам не заводит, а заявка даёт
/// администратору всё необходимое, чтобы завести его осознанно.
/// </remarks>
[ApiController]
[Route("api/v1/enroll")]
[AllowAnonymous]
[Produces("text/plain")]
public class EnrollController(IEnrollmentService enrollmentService) : ApiControllerBase
{
    /// <summary>
    /// Подаёт заявку на регистрацию компьютера.
    /// </summary>
    /// <param name="request">Сведения о компьютере и пользователе.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Идентификатор и состояние заявки.</returns>
    /// <response code="200">Заявка принята либо обновлена ранее поданная.</response>
    /// <response code="400">Не указан идентификатор компьютера.</response>
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    public async Task<IActionResult> Submit([FromForm] EnrollRequestDto request, CancellationToken cancellationToken)
    {
        var entity = await enrollmentService.SubmitAsync(
            new EnrollmentSubmission(
                request.ClientId,
                request.HardwareFingerprint,
                request.Hostname,
                request.OsVersion,
                request.Username,
                request.Comment),
            Connection.RemoteIpAddress,
            cancellationToken);

        return TextPairs(
            ("status", "ok"),
            ("request_id", entity.Id),
            ("state", entity.Status.ToString()),
            ("message", "Заявка передана администратору. Повторите вход после её одобрения"));
    }
}
