using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UpdateHub.Server.Api.V1.DTOs.Request;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Application.Sync;

namespace UpdateHub.Server.Api.V1.Controllers;

/// <summary>
/// Вход в систему и работа с токенами.
/// </summary>
/// <param name="authService">Служба авторизации.</param>
/// <param name="clientService">Управление компьютерами.</param>
/// <remarks>
/// Все ответы — текст вида «ключ=значение», чтобы bash-клиенту хватало
/// <c>curl</c> и <c>awk</c> без установки <c>jq</c>.
/// </remarks>
[ApiController]
[Route("api/v1/auth")]
[Produces("text/plain")]
public class AuthController(
    IAuthService authService,
    IClientService clientService) : ApiControllerBase
{
    /// <summary>
    /// Проверяет учётные данные и выдаёт пару токенов.
    /// </summary>
    /// <param name="request">Логин, пароль, идентификатор компьютера и сведения о нём.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Строки «ключ=значение» с токенами и сведениями о пользователе.</returns>
    /// <response code="200">Вход выполнен.</response>
    /// <response code="400">Не заполнены обязательные поля.</response>
    /// <response code="401">Неверные учётные данные либо нет прав на компьютер.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    public async Task<IActionResult> Login([FromForm] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(
            request.Username,
            request.Password,
            request.ClientId,
            Connection,
            cancellationToken);

        // Сведения о железе обновляются после успешного входа: до проверки прав
        // сервер не должен ничего записывать о неизвестном компьютере.
        await clientService.RecordCheckInAsync(
            request.ClientId,
            new ClientReport(
                request.Hostname,
                request.HardwareFingerprint,
                request.OsVersion,
                request.KernelVersion,
                request.Architecture,
                request.CpuInfo,
                request.MemoryGb,
                request.DiskGb,
                request.MacAddress),
            Connection,
            cancellationToken);

        return TextPairs(
            ("access_token", result.AccessToken),
            ("refresh_token", result.RefreshToken),
            ("expires_in", result.ExpiresInSeconds.ToString()),
            ("user_id", result.UserId),
            ("username", result.Username),
            ("role", result.Role),
            ("client_id", result.ClientId),
            ("must_change_password", result.MustChangePassword ? "1" : "0"));
    }

    /// <summary>
    /// Обменивает refresh-токен на новую пару токенов.
    /// </summary>
    /// <param name="request">Действующий refresh-токен.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Строки «ключ=значение» с новой парой токенов.</returns>
    /// <response code="200">Токены обновлены.</response>
    /// <response code="401">Токен недействителен, истёк или уже отозван.</response>
    /// <remarks>
    /// Прежний refresh-токен отзывается. Скрипту нужно сохранить новый:
    /// при семичасовой закачке access-токен успевает истечь несколько раз.
    /// </remarks>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    public async Task<IActionResult> Refresh([FromForm] RefreshRequestDto request, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request.RefreshToken, Connection, cancellationToken);

        return TextPairs(
            ("access_token", result.AccessToken),
            ("refresh_token", result.RefreshToken),
            ("expires_in", result.ExpiresInSeconds.ToString()),
            ("user_id", result.UserId),
            ("username", result.Username),
            ("role", result.Role));
    }

    /// <summary>
    /// Отзывает refresh-токен.
    /// </summary>
    /// <param name="request">Отзываемый токен.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Пустой ответ.</returns>
    /// <response code="204">Токен отозван либо уже был недействителен.</response>
    /// <response code="401">Запрос без действующего access-токена.</response>
    [HttpPost("logout")]
    [Authorize]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    public async Task<IActionResult> Logout([FromForm] RefreshRequestDto request, CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request.RefreshToken, CurrentUserId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Меняет пароль текущего пользователя.
    /// </summary>
    /// <param name="request">Текущий и новый пароли.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Подтверждение смены.</returns>
    /// <response code="200">Пароль изменён, все refresh-токены отозваны.</response>
    /// <response code="400">Новый пароль не удовлетворяет требованиям.</response>
    /// <response code="401">Текущий пароль указан неверно.</response>
    [HttpPost("change-password")]
    [Authorize]
    [Consumes("application/x-www-form-urlencoded", "multipart/form-data")]
    public async Task<IActionResult> ChangePassword(
        [FromForm] ChangePasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        await authService.ChangePasswordAsync(
            CurrentUserId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);

        return TextPairs(
            ("status", "ok"),
            ("message", "Пароль изменён, требуется повторный вход"));
    }
}
