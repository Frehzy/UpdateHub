using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Headers;
using UpdateHub.BackendServer.Application.BackgroundServices;
using UpdateHub.BackendServer.Domain.Entities.Clients;
using UpdateHub.BackendServer.Domain.Entities.Users;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.BackendServer.Infrastructure.Database;
using UpdateHub.BackendServer.Infrastructure.Security;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Backend.Tests.TestSupport;

/// <summary>
/// Поднимает приложение целиком для проверки контроллеров.
/// </summary>
/// <remarks>
/// В отличие от остальных тестов, здесь не создаётся ни одного объекта
/// вручную: работает настоящая маршрутизация, настоящая привязка формы,
/// настоящая проверка токена и настоящая роль. Ровно эти вещи не покрываются
/// проверкой служб по отдельности, а ломаются чаще всего — опечатка в маршруте
/// или забытый атрибут не видны ни компилятору, ни модульным тестам.
/// <para>
/// Приложение читает настройки при регистрации служб — ключ подписи проверяется
/// прямо там и при отсутствии роняет запуск. Поэтому значения задаются
/// переменными окружения в статическом конструкторе: они попадают в конфигурацию
/// раньше любого кода приложения. Стоимость хэширования занижена намеренно,
/// иначе каждый вход добавлял бы к прогону треть секунды.
/// </para>
/// <para>
/// База — настоящий файл SQLite во временном каталоге, а не подмена в памяти:
/// так заодно проверяется штатная подготовка базы при старте, включая
/// применение миграций.
/// </para>
/// </remarks>
public sealed class UpdateHubApplication : WebApplicationFactory<Program>
{
    /// <summary>Ключ подписи токенов длиной не меньше 32 байт.</summary>
    private const string TestSecret = "kluch-dlya-integratsionnyh-testov-1234567890";

    /// <summary>Каталог, в котором живут файлы и база одного прогона.</summary>
    private static readonly string Root =
        Path.Combine(Path.GetTempPath(), $"updatehub-tests-{Guid.NewGuid():N}");

    /// <summary>Пароль первого администратора, заведённого при подготовке базы.</summary>
    public const string AdminPassword = "admin-parol-12345";

    /// <summary>Логин первого администратора.</summary>
    public const string AdminUsername = "admin";

    /// <summary>Каталог раздачи, из которого сервер отдаёт файлы.</summary>
    public static string FilesPath { get; } = Path.Combine(Root, "files");

    /// <summary>Задаёт настройки до того, как приложение их прочитает.</summary>
    static UpdateHubApplication()
    {
        Directory.CreateDirectory(FilesPath);

        Environment.SetEnvironmentVariable("Jwt__SecretKey", TestSecret);
        Environment.SetEnvironmentVariable("Security__PasswordWorkFactor", "4");
        Environment.SetEnvironmentVariable("BootstrapAdmin__Username", AdminUsername);
        Environment.SetEnvironmentVariable("BootstrapAdmin__Password", AdminPassword);
        Environment.SetEnvironmentVariable("UpdateHub__FilesPath", FilesPath);
        Environment.SetEnvironmentVariable("UpdateHub__DatabasePath", Path.Combine(Root, "data", "updatehub.db"));

        // Обычно сканер пропускает файлы, изменённые за последние пятнадцать секунд:
        // они ещё могут дописываться, и хэш получился бы от половины файла. В тесте
        // файл создаётся и тут же запрашивается, поэтому выдержка не нужна.
        Environment.SetEnvironmentVariable("UpdateHub__FileSettleSeconds", "0");
    }

    /// <inheritdoc />
    /// <remarks>
    /// Фоновые задачи отключаются: обход каталога по таймеру перезаписывал бы
    /// манифест в непредсказуемый момент, и тест, положивший файл и сразу
    /// запросивший манифест, падал бы через раз. Обход запускается тестом явно.
    /// </remarks>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var background = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(IHostedService) &&
                    (descriptor.ImplementationType == typeof(ManifestScanBackgroundService) ||
                     descriptor.ImplementationType == typeof(CleanupBackgroundService)))
                .ToList();

            foreach (var descriptor in background)
            {
                services.Remove(descriptor);
            }
        });
    }

    /// <summary>Создаёт клиента без автоматического перехода по перенаправлениям.</summary>
    /// <returns>Клиент к поднятому приложению.</returns>
    /// <remarks>
    /// Перенаправления отключены намеренно: bash-скрипт на клиенте ходит
    /// обычным <c>curl</c> без <c>-L</c>, и ответ 301 для него — ошибка,
    /// а не повод сходить ещё раз.
    /// </remarks>
    public HttpClient CreateApiClient()
        => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>
    /// Выполняет вход и возвращает клиента с уже подставленным токеном.
    /// </summary>
    /// <param name="username">Логин.</param>
    /// <param name="password">Пароль.</param>
    /// <param name="clientId">Идентификатор компьютера или <c>null</c> для входа без привязки.</param>
    /// <returns>Клиент, добавляющий заголовок авторизации к каждому запросу.</returns>
    public async Task<HttpClient> CreateAuthorizedClientAsync(string username, string password, string? clientId = null)
    {
        var client = CreateApiClient();
        var fields = new Dictionary<string, string> { ["username"] = username, ["password"] = password };

        if (clientId is not null)
        {
            fields["client_id"] = clientId;
        }

        var response = await client.PostAsync("/api/v1/auth/login", new FormUrlEncodedContent(fields));
        response.EnsureSuccessStatusCode();

        var pairs = ParseTextPairs(await response.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pairs["access_token"]);

        return client;
    }

    /// <summary>Возвращает клиента, вошедшего первым администратором.</summary>
    /// <returns>Клиент с токеном администратора.</returns>
    public Task<HttpClient> CreateAdminClientAsync()
        => CreateAuthorizedClientAsync(AdminUsername, AdminPassword);

    /// <summary>
    /// Разбирает ответ вида «ключ=значение» — так отвечает клиентская часть API.
    /// </summary>
    /// <param name="text">Тело ответа.</param>
    /// <returns>Пары «ключ — значение».</returns>
    public static Dictionary<string, string> ParseTextPairs(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = line.IndexOf('=');
            if (separator > 0)
            {
                result[line[..separator]] = line[(separator + 1)..];
            }
        }

        return result;
    }

    /// <summary>
    /// Выполняет действие над базой поднятого приложения.
    /// </summary>
    /// <param name="action">Действие над контекстом.</param>
    /// <remarks>
    /// Нужно для подготовки данных, которых нет ни в одном эндпоинте: например,
    /// пользователя с обычной ролью и выданным доступом к компьютеру.
    /// </remarks>
    public async Task WithDatabaseAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>
    /// Заводит пользователя напрямую в базе.
    /// </summary>
    /// <param name="username">Логин.</param>
    /// <param name="password">Пароль.</param>
    /// <param name="role">Роль.</param>
    /// <returns>Созданный пользователь.</returns>
    public async Task<UserEntity> AddUserAsync(string username, string password, UserRole role = UserRole.Client)
    {
        UserEntity user = null!;

        await WithDatabaseAsync(async context =>
        {
            var hasher = Services.GetRequiredService<PasswordHasher>();
            user = new UserEntity
            {
                Username = username,
                PasswordHash = hasher.HashPassword(password),
                Role = role,
                IsActive = true
            };

            context.Users.Add(user);
            await context.SaveChangesAsync();
        });

        return user;
    }

    /// <summary>
    /// Заводит компьютер и, если указан пользователь, выдаёт ему доступ.
    /// </summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="userId">Пользователь, которому выдать доступ.</param>
    /// <returns>Созданный компьютер.</returns>
    public async Task<ClientEntity> AddClientAsync(string clientId, string? userId = null)
    {
        var client = new ClientEntity { Id = clientId, IsActive = true };

        await WithDatabaseAsync(async context =>
        {
            context.Clients.Add(client);

            if (userId is not null)
            {
                context.UserClientAccesses.Add(new UserClientAccessEntity { UserId = userId, ClientId = clientId });
            }

            await context.SaveChangesAsync();
        });

        return client;
    }

    /// <summary>
    /// Кладёт файл в каталог раздачи и запускает обход, чтобы он попал в манифест.
    /// </summary>
    /// <param name="relativePath">Путь относительно каталога раздачи.</param>
    /// <param name="content">Содержимое файла.</param>
    /// <returns>Задача завершения.</returns>
    /// <remarks>
    /// Кодировка не задаётся явно: с указанием <c>Encoding.UTF8</c> запись
    /// начинается с метки порядка байтов, и размер файла перестаёт совпадать
    /// с длиной строки, а контрольная сумма — с посчитанной вручную.
    /// </remarks>
    public async Task PublishFileAsync(string relativePath, string content)
    {
        var fullPath = Path.Combine(FilesPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content);

        using var admin = await CreateAdminClientAsync();
        var response = await admin.PostAsync("/api/v1/admin/manifest/rescan", content: null);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(Root))
        {
            Directory.Delete(Root, recursive: true);
        }
    }
}
