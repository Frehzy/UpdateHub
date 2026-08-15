using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Infrastructure.Middleware;

public class ClientAccessMiddleware(RequestDelegate next, ILogger<ClientAccessMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        IClientService clientService,
        IAuthService authService)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Пропускаем эндпоинты без проверки доступа к клиенту
        if (path == "/health" ||
            path == "/api/v1/auth/login" ||
            path == "/api/v1/auth/refresh" ||
            path == "/api/v1/auth/logout" ||
            path == "/api/v1/auth/change-password" ||
            path.StartsWith("/api/v1/admin"))
        {
            await next(context);
            return;
        }

        var userId = context.Items["UserId"]?.ToString();
        var userRole = context.Items["UserRole"]?.ToString();

        if (string.IsNullOrEmpty(userId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("{\"error\":\"User not authenticated\"}");
            return;
        }

        // Администратор имеет доступ ко всем клиентам
        if (userRole == UserRole.Admin.ToString())
        {
            await next(context);
            return;
        }

        // Для обычных пользователей проверяем доступ к клиенту
        var clientId = ExtractClientId(context);

        if (string.IsNullOrEmpty(clientId))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("{\"error\":\"Client ID is required\"}");
            return;
        }

        // Проверяем, существует ли клиент
        var client = await clientService.GetClientByIdAsync(clientId);
        if (client == null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync("{\"error\":\"Client not registered. Please contact administrator.\"}");
            return;
        }

        // Проверяем доступ пользователя к клиенту
        var hasAccess = await authService.HasAccessToClientAsync(userId, clientId);
        if (!hasAccess)
        {
            logger.LogWarning("User {UserId} attempted to access client {ClientId} without permission", userId, clientId);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("{\"error\":\"Access denied to this client\"}");
            return;
        }

        // Сохраняем ClientId в HttpContext для дальнейшего использования
        context.Items["ClientId"] = clientId;

        await next(context);
    }

    private static string? ExtractClientId(HttpContext context)
    {
        // Пытаемся извлечь client_uuid из тела запроса
        // Для простоты используем чтение из формы или query
        if (context.Request.HasFormContentType)
        {
            var form = context.Request.Form;
            if (form.TryGetValue("client_uuid", out var value))
            {
                return value.ToString();
            }
        }

        // Также можно искать в query string
        if (context.Request.Query.TryGetValue("client_uuid", out var queryValue))
        {
            return queryValue.ToString();
        }

        // Для JSON запросов потребуется более сложный парсинг
        // В реальном проекте лучше использовать привязку модели
        return null;
    }
}