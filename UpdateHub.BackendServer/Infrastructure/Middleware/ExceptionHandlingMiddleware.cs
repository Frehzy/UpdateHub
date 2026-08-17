using System.Text.Json;
using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.Shared.Contracts;

namespace UpdateHub.BackendServer.Infrastructure.Middleware;

/// <summary>
/// Превращает исключения прикладного слоя в осмысленные коды ответа.
/// </summary>
/// <param name="next">Следующий обработчик конвейера.</param>
/// <param name="logger">Журнал.</param>
/// <remarks>
/// Клиентская часть API отвечает текстом, панель управления — JSON,
/// поэтому формат ответа выбирается по пути запроса. Так bash-клиенту
/// не приходится разбирать JSON ради сообщения об ошибке.
/// </remarks>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>Обрабатывает запрос, перехватывая необработанные исключения.</summary>
    /// <param name="context">Контекст запроса.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleAsync(context, ex);
        }
    }

    /// <summary>
    /// Формирует ответ об ошибке.
    /// </summary>
    /// <param name="context">Контекст запроса.</param>
    /// <param name="exception">Перехваченное исключение.</param>
    private async Task HandleAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, logAsError) = Classify(exception);

        if (logAsError)
        {
            logger.LogError(exception, "Необработанная ошибка при обработке {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            logger.LogWarning("{Method} {Path}: {Message}",
                context.Request.Method, context.Request.Path, message);
        }

        // Если ответ уже начал отправляться — например, оборвалась отдача
        // большого файла, — заголовки менять поздно.
        if (context.Response.HasStarted)
        {
            logger.LogWarning("Ответ уже начал отправляться, изменить код состояния невозможно");
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;

        var isTextApi = !context.Request.Path.StartsWithSegments("/api/v1/admin");

        if (isTextApi)
        {
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync($"error={message}\n");
        }
        else
        {
            context.Response.ContentType = "application/json; charset=utf-8";
            var payload = JsonSerializer.Serialize(
                new ErrorResponseDto { Error = message },
                JsonSerializerOptions.Web);
            await context.Response.WriteAsync(payload);
        }
    }

    /// <summary>
    /// Сопоставляет исключение с кодом ответа и сообщением.
    /// </summary>
    /// <param name="exception">Исключение.</param>
    /// <returns>Код состояния, сообщение и признак записи в журнал как ошибки.</returns>
    private static (int StatusCode, string Message, bool LogAsError) Classify(Exception exception) => exception switch
    {
        EntityNotFoundException => (StatusCodes.Status404NotFound, exception.Message, false),
        AuthenticationFailedException => (StatusCodes.Status401Unauthorized, exception.Message, false),
        AccessDeniedException => (StatusCodes.Status403Forbidden, exception.Message, false),
        InvalidOperationException => (StatusCodes.Status409Conflict, exception.Message, false),
        ArgumentException => (StatusCodes.Status400BadRequest, exception.Message, false),

        // Клиент закрыл соединение, не дождавшись ответа. На семичасовой закачке
        // по каналу 2 Мбит/с это обычное дело и ошибкой сервера не является.
        OperationCanceledException => (ClientClosedRequest, "Запрос отменён клиентом", false),

        _ => (StatusCodes.Status500InternalServerError, "Внутренняя ошибка сервера", true)
    };

    /// <summary>Нестандартный код «клиент закрыл соединение», введённый nginx.</summary>
    private const int ClientClosedRequest = 499;
}
