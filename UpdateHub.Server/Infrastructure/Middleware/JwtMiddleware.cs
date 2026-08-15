using System.Security.Claims;
using UpdateHub.Server.Infrastructure.Security;

namespace UpdateHub.Server.Infrastructure.Middleware;

public class JwtMiddleware(RequestDelegate next, ILogger<JwtMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, TokenGenerator tokenGenerator)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Пропускаем эндпоинты без авторизации
        if (path == "/health" ||
            path == "/api/v1/auth/login" ||
            path == "/api/v1/auth/refresh")
        {
            await next(context);
            return;
        }

        var token = context.Request.Headers.Authorization.ToString().Replace("Bearer ", "");

        if (string.IsNullOrEmpty(token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("{\"error\":\"Missing authorization token\"}");
            return;
        }

        var (isValid, principal) = tokenGenerator.ValidateAccessToken(token);

        if (!isValid)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("{\"error\":\"Invalid or expired token\"}");
            return;
        }

        // Сохраняем информацию о пользователе в HttpContext
        context.Items["UserId"] = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        context.Items["UserRole"] = principal?.FindFirst(ClaimTypes.Role)?.Value;
        context.Items["Username"] = principal?.FindFirst(ClaimTypes.Name)?.Value;

        await next(context);
    }
}