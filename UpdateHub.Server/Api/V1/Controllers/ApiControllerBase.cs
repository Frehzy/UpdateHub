using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using UpdateHub.Server.Application.Sync;
using UpdateHub.Server.Domain.Enums;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Server.Api.V1.Controllers;

/// <summary>
/// Общая основа контроллеров: сведения о текущем пользователе и соединении,
/// а также сборка текстовых ответов для bash-клиента.
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Идентификатор текущего пользователя из access-токена.</summary>
    protected string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>Логин текущего пользователя из access-токена.</summary>
    protected string CurrentUsername => User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    /// <summary>Является ли текущий пользователь администратором.</summary>
    protected bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    /// <summary>
    /// Сведения о соединении: адрес берётся из самого соединения,
    /// а не из тела запроса, поэтому клиент не может его подделать.
    /// </summary>
    protected ConnectionContext Connection => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString() is { Length: > 0 } agent ? agent : null);

    /// <summary>
    /// Формирует ответ в виде строк «ключ=значение».
    /// </summary>
    /// <param name="pairs">Пары «ключ — значение»; пустые значения пропускаются.</param>
    /// <returns>Текстовый ответ.</returns>
    /// <remarks>
    /// Такой формат разбирается в bash одной командой <c>awk -F=</c>
    /// и не требует установленного <c>jq</c>, которого в закрытом контуре
    /// может не оказаться.
    /// </remarks>
    protected ContentResult TextPairs(params (string Key, string? Value)[] pairs)
    {
        var builder = new StringBuilder();

        foreach (var (key, value) in pairs)
        {
            if (value is null)
            {
                continue;
            }

            builder.Append(key).Append('=').Append(value).Append('\n');
        }

        return Content(builder.ToString(), "text/plain; charset=utf-8");
    }

    /// <summary>
    /// Формирует текстовый ответ об ошибке для bash-клиента.
    /// </summary>
    /// <param name="statusCode">Код состояния HTTP.</param>
    /// <param name="message">Сообщение на русском языке.</param>
    /// <returns>Текстовый ответ с заданным кодом.</returns>
    protected ContentResult TextError(int statusCode, string message)
    {
        Response.StatusCode = statusCode;
        return Content($"error={message}\n", "text/plain; charset=utf-8");
    }
}
