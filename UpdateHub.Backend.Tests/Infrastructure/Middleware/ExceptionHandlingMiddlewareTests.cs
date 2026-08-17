using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using UpdateHub.BackendServer.Application.Services.Updates;
using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Infrastructure.Middleware;

namespace UpdateHub.Backend.Tests.Infrastructure.Middleware;

/// <summary>
/// Проверяет превращение исключений в ответы.
/// </summary>
/// <remarks>
/// Слой на первый взгляд служебный, но именно он определяет, что увидит
/// bash-скрипт при любой нештатной ситуации. Два свойства здесь важнее
/// остальных. Первое: формат ответа зависит от адреса — клиентская часть
/// получает текст, панель управления JSON; клиент разбирает ответ без jq,
/// и JSON он прочитать не сможет. Второе: код состояния должен отличать
/// «нет прав» от «не найдено» и от поломки сервера, иначе скрипт не сможет
/// решить, повторять ли попытку.
/// </remarks>
public class ExceptionHandlingMiddlewareTests
{
    /// <summary>Пропускает запрос через слой, заставив следующий шаг упасть.</summary>
    /// <param name="path">Адрес запроса.</param>
    /// <param name="exception">Исключение, которое возникнет при обработке.</param>
    /// <returns>Код состояния, тип содержимого и тело ответа.</returns>
    private static async Task<(int StatusCode, string? ContentType, string Body)> HandleAsync(
        string path,
        Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = HttpMethods.Get;
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw exception,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);

        return (context.Response.StatusCode, context.Response.ContentType, await reader.ReadToEndAsync());
    }

    /// <summary>Успешный запрос слой не трогает.</summary>
    [Fact]
    public async Task SuccessfulRequest_PassesThrough()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var called = false;

        var middleware = new ExceptionHandlingMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.True(called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    /// <summary>Каждому виду ошибки соответствует свой код состояния.</summary>
    /// <remarks>
    /// Отдельно проверяется 409 для <c>InvalidOperationException</c>: им служба
    /// сообщает о попытке завести то, что уже есть, — например группу
    /// с занятым названием. Отвечать на это пятисоткой было бы неверно:
    /// сервер исправен, ошибся вызывающий.
    /// </remarks>
    [Fact]
    public async Task ExceptionKind_DefinesStatusCode()
    {
        var notFound = await HandleAsync("/api/v1/sync/manifest", new EntityNotFoundException("не найдено"));
        var unauthorized = await HandleAsync("/api/v1/auth/login", new AuthenticationFailedException("не пущу"));
        var forbidden = await HandleAsync("/api/v1/files", new AccessDeniedException("нет прав"));
        var conflict = await HandleAsync("/api/v1/admin/groups", new InvalidOperationException("уже есть"));
        var badRequest = await HandleAsync("/api/v1/sync/diff", new ArgumentException("плохой параметр"));

        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    /// <summary>
    /// Непредвиденное исключение превращается в 500 без подробностей.
    /// </summary>
    /// <remarks>
    /// Текст исключения может содержать пути, имена таблиц и куски запросов.
    /// Клиенту достаточно знать, что виноват сервер; подробности остаются
    /// в журнале.
    /// </remarks>
    [Fact]
    public async Task UnexpectedException_HidesDetails()
    {
        var result = await HandleAsync(
            "/api/v1/sync/manifest",
            new NullReferenceException("Object reference not set в SyncService.BuildPlanAsync"));

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
        Assert.DoesNotContain("SyncService", result.Body, StringComparison.Ordinal);
        Assert.Contains("error=Внутренняя ошибка сервера", result.Body, StringComparison.Ordinal);
    }

    /// <summary>Клиентская часть получает ответ текстом вида «error=сообщение».</summary>
    [Fact]
    public async Task ClientApi_AnswersWithPlainText()
    {
        var result = await HandleAsync("/api/v1/sync/manifest", new EntityNotFoundException("Файл не найден"));

        Assert.StartsWith("text/plain", result.ContentType, StringComparison.Ordinal);
        Assert.Equal("error=Файл не найден\n", result.Body);
    }

    /// <summary>Панель управления получает ответ в JSON.</summary>
    [Fact]
    public async Task AdminApi_AnswersWithJson()
    {
        var result = await HandleAsync("/api/v1/admin/users", new EntityNotFoundException("Пользователь не найден"));

        Assert.StartsWith("application/json", result.ContentType, StringComparison.Ordinal);

        var payload = JsonSerializer.Deserialize<JsonElement>(result.Body);
        Assert.Equal("Пользователь не найден", payload.GetProperty("error").GetString());
    }

    /// <summary>
    /// Обрыв соединения клиентом отвечает кодом 499, а не пятисоткой.
    /// </summary>
    /// <remarks>
    /// Отдача шестигигабайтного образа по каналу 2 Мбит/с занимает около семи
    /// часов, и обрыв на такой дистанции — рядовое событие. Считать его
    /// ошибкой сервера значит утопить журнал в ложных тревогах.
    /// </remarks>
    [Fact]
    public async Task ClientDisconnect_NotTreatedAsServerError()
    {
        var result = await HandleAsync("/api/v1/files", new OperationCanceledException());

        Assert.Equal(499, result.StatusCode);
    }

    /// <summary>
    /// Если ответ уже начал уходить, слой не пытается переписать заголовки.
    /// </summary>
    /// <remarks>
    /// Ровно так выглядит обрыв на середине отдачи большого файла: часть тела
    /// уже у клиента, менять код состояния поздно, и попытка это сделать
    /// закончилась бы вторым исключением поверх первого.
    /// </remarks>
    [Fact]
    public async Task ResponseAlreadyStarted_LeavesStatusUnchanged()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/files";
        context.Response.Body = new MemoryStream();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException("обрыв на середине"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    /// <summary>
    /// Ответ, который уже начал отправляться.
    /// </summary>
    /// <remarks>
    /// Обычная реализация в тестовом контексте всегда сообщает, что отправка
    /// не начиналась, поэтому нужна своя.
    /// </remarks>
    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        /// <inheritdoc />
        public int StatusCode { get; set; } = StatusCodes.Status200OK;

        /// <inheritdoc />
        public string? ReasonPhrase { get; set; }

        /// <inheritdoc />
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        /// <inheritdoc />
        public Stream Body { get; set; } = new MemoryStream();

        /// <inheritdoc />
        public bool HasStarted => true;

        /// <inheritdoc />
        public void OnStarting(Func<object, Task> callback, object state)
        {
        }

        /// <inheritdoc />
        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }
    }
}
