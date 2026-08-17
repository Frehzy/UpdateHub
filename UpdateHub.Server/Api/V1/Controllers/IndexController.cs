using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace UpdateHub.Server.Api.V1.Controllers;

/// <summary>
/// Справка по составу API.
/// </summary>
/// <remarks>
/// Существует потому, что базовые адреса разделов (<c>/api/v1/auth</c>,
/// <c>/api/v1/admin</c>) сами по себе действий не содержат и в браузере отдают
/// 404 без каких-либо пояснений. Этот эндпоинт даёт человеку, открывшему сервер
/// в браузере, понятную отправную точку вместо пустой страницы с ошибкой.
/// </remarks>
[ApiController]
[AllowAnonymous]
[Produces("text/plain")]
public class IndexController : ControllerBase
{
    /// <summary>
    /// Возвращает список доступных разделов API.
    /// </summary>
    /// <returns>Текстовая справка.</returns>
    /// <response code="200">Справка возвращена.</response>
    [HttpGet("/api")]
    [HttpGet("/api/v1")]
    public IActionResult Get()
    {
        var text = new StringBuilder()
            .Append("UpdateHub — сервер обновлений\n")
            .Append('\n')
            .Append("Клиентская часть (ответы текстом, для bash-скрипта):\n")
            .Append("  POST /api/v1/auth/login             вход, форма: username, password, client_id\n")
            .Append("  POST /api/v1/auth/refresh           обновление токенов, форма: refresh_token\n")
            .Append("  POST /api/v1/auth/logout            отзыв refresh-токена\n")
            .Append("  POST /api/v1/auth/change-password   смена пароля\n")
            .Append("  POST /api/v1/sync/diff              сравнение манифестов, тело: вывод md5sum\n")
            .Append("  GET  /api/v1/sync/manifest          эталонный манифест целиком\n")
            .Append("  GET  /api/v1/files                  скачивание файла: ?client_id=...&path=...\n")
            .Append("  POST /api/v1/enroll                 заявка на регистрацию компьютера\n")
            .Append('\n')
            .Append("Панель управления (ответы JSON, требуется роль Admin):\n")
            .Append("  GET  /api/v1/admin/users            пользователи\n")
            .Append("  GET  /api/v1/admin/clients          компьютеры\n")
            .Append("  GET  /api/v1/admin/groups           группы компьютеров\n")
            .Append("  GET  /api/v1/admin/enrollments      заявки на регистрацию\n")
            .Append("  GET  /api/v1/admin/manifest/status  состояние манифеста и каталога раздачи\n")
            .Append("  POST /api/v1/admin/manifest/rescan  внеочередной обход каталога\n")
            .Append("  GET  /api/v1/admin/stats            статистика обращений\n")
            .Append('\n')
            .Append("Служебное:\n")
            .Append("  GET  /                              панель управления в браузере\n")
            .Append("  GET  /health                        проверка работоспособности\n")
            .Append("  GET  /swagger                       описание API (только в режиме разработки)\n")
            .Append('\n')
            .Append("Все адреса, кроме /api/v1/auth/login, /api/v1/auth/refresh, /api/v1/enroll,\n")
            .Append("/health и этой страницы, требуют заголовок Authorization: Bearer <токен>.\n")
            .Append("Открыть их в браузере без токена не получится — используйте curl или Swagger.\n")
            .ToString();

        return Content(text, "text/plain; charset=utf-8");
    }
}
