using Microsoft.JSInterop;
using UpdateHub.Shared.Enums;

namespace UpdateHub.FrontendServer.Services;

/// <summary>
/// Состояние входа: токены, сведения о вошедшем и вход с выходом.
/// </summary>
/// <remarks>
/// Токены хранятся в <c>localStorage</c>, чтобы обновление страницы не
/// выбрасывало администратора обратно на форму входа. Для сервера в закрытом
/// контуре этого достаточно: он стоит за межсетевым экраном и криптомаршрутизатором,
/// а панель открывают с той же машины или из её сети.
/// <para>
/// Access-токен живёт недолго и обновляется по refresh-токену. Обновление
/// выполняет <see cref="ApiClient"/>, когда сервер отвечает 401.
/// </para>
/// </remarks>
/// <param name="http">Клиент HTTP.</param>
/// <param name="js">Выполнение JavaScript для доступа к хранилищу браузера.</param>
public sealed class AuthState(HttpClient http, IJSRuntime js)
{
    private const string AccessTokenKey = "updatehub.access-token";
    private const string RefreshTokenKey = "updatehub.refresh-token";

    /// <summary>Действующий access-токен.</summary>
    public string? AccessToken { get; private set; }

    /// <summary>Действующий refresh-токен.</summary>
    public string? RefreshToken { get; private set; }

    /// <summary>Логин вошедшего.</summary>
    public string? Username { get; private set; }

    /// <summary>Роль вошедшего.</summary>
    public string? Role { get; private set; }

    /// <summary>Требуется ли смена пароля до начала работы.</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>Завершено ли восстановление состояния из хранилища браузера.</summary>
    public bool IsReady { get; private set; }

    /// <summary>Выполнен ли вход.</summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

    /// <summary>Является ли вошедший администратором.</summary>
    public bool IsAdmin => Role == nameof(UserRole.Admin);

    /// <summary>Происходит при любом изменении состояния входа.</summary>
    public event Action? Changed;

    /// <summary>
    /// Восстанавливает состояние из хранилища браузера.
    /// </summary>
    /// <returns>Задача завершения.</returns>
    /// <remarks>
    /// Сохранённый access-токен мог протухнуть, пока вкладка была закрыта,
    /// поэтому состояние восстанавливается обновлением по refresh-токену:
    /// заодно приходят свежие логин и роль, и панель не показывает
    /// администратору чужие пункты меню по устаревшим данным.
    /// </remarks>
    public async Task InitializeAsync()
    {
        RefreshToken = await ReadAsync(RefreshTokenKey);
        AccessToken = await ReadAsync(AccessTokenKey);

        if (!string.IsNullOrEmpty(RefreshToken))
        {
            await TryRefreshAsync();
        }

        IsReady = true;
        Changed?.Invoke();
    }

    /// <summary>
    /// Выполняет вход.
    /// </summary>
    /// <param name="username">Логин.</param>
    /// <param name="password">Пароль.</param>
    /// <returns>Сообщение об ошибке или <c>null</c> при успехе.</returns>
    public async Task<string?> LoginAsync(string username, string password)
    {
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password
        });

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync("api/v1/auth/login", form);
        }
        catch (HttpRequestException)
        {
            return "Сервер недоступен";
        }

        var text = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return TextProtocol.ExtractError(text, "Не удалось войти");
        }

        var pairs = TextProtocol.Parse(text);

        // Панель управления доступна только администратору. Обычный пользователь
        // с верным паролем получил бы отказ на каждом запросе, и разбираться
        // в этом ему пришлось бы по пустым спискам.
        if (pairs.GetValueOrDefault("role") != nameof(UserRole.Admin))
        {
            return "Панель управления доступна только администратору";
        }

        await ApplyAsync(pairs);
        return null;
    }

    /// <summary>
    /// Обновляет access-токен по refresh-токену.
    /// </summary>
    /// <returns><c>true</c>, если токен обновлён.</returns>
    public async Task<bool> TryRefreshAsync()
    {
        if (string.IsNullOrEmpty(RefreshToken))
        {
            return false;
        }

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["refresh_token"] = RefreshToken
        });

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsync("api/v1/auth/refresh", form);
        }
        catch (HttpRequestException)
        {
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            await LogoutAsync();
            return false;
        }

        await ApplyAsync(TextProtocol.Parse(await response.Content.ReadAsStringAsync()));
        return true;
    }

    /// <summary>
    /// Сообщает серверу об окончании работы и очищает состояние.
    /// </summary>
    /// <returns>Задача завершения.</returns>
    /// <remarks>
    /// Отзыв refresh-токена на сервере выполняется по возможности: если сервер
    /// недоступен, локальное состояние всё равно нужно очистить, иначе панель
    /// останется «наполовину вошедшей».
    /// </remarks>
    public async Task LogoutAsync()
    {
        if (!string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(RefreshToken))
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/logout")
                {
                    Content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["refresh_token"] = RefreshToken
                    })
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);

                await http.SendAsync(request);
            }
            catch (HttpRequestException)
            {
                // Сервер недоступен — токен отзовётся по истечении срока.
            }
        }

        AccessToken = null;
        RefreshToken = null;
        Username = null;
        Role = null;
        MustChangePassword = false;

        await RemoveAsync(AccessTokenKey);
        await RemoveAsync(RefreshTokenKey);

        Changed?.Invoke();
    }

    /// <summary>
    /// Отмечает, что пароль сменён и требование больше не действует.
    /// </summary>
    public void MarkPasswordChanged()
    {
        MustChangePassword = false;
        Changed?.Invoke();
    }

    /// <summary>Сохраняет разобранный ответ входа.</summary>
    /// <param name="pairs">Пары «ключ — значение» из ответа.</param>
    /// <returns>Задача завершения.</returns>
    private async Task ApplyAsync(Dictionary<string, string> pairs)
    {
        AccessToken = pairs.GetValueOrDefault("access_token");
        RefreshToken = pairs.GetValueOrDefault("refresh_token");
        Username = pairs.GetValueOrDefault("username");
        Role = pairs.GetValueOrDefault("role");
        MustChangePassword = pairs.GetValueOrDefault("must_change_password") == "1";

        await WriteAsync(AccessTokenKey, AccessToken);
        await WriteAsync(RefreshTokenKey, RefreshToken);

        Changed?.Invoke();
    }

    /// <summary>Читает значение из хранилища браузера.</summary>
    /// <param name="key">Ключ.</param>
    /// <returns>Значение или <c>null</c>.</returns>
    private async Task<string?> ReadAsync(string key)
    {
        try
        {
            var value = await js.InvokeAsync<string?>("localStorage.getItem", key);
            return string.IsNullOrEmpty(value) ? null : value;
        }
        catch (JSException)
        {
            // Хранилище может быть запрещено настройками браузера. Панель
            // продолжит работать, просто вход придётся выполнять заново.
            return null;
        }
    }

    /// <summary>Записывает значение в хранилище браузера.</summary>
    /// <param name="key">Ключ.</param>
    /// <param name="value">Значение.</param>
    /// <returns>Задача завершения.</returns>
    private async Task WriteAsync(string key, string? value)
    {
        try
        {
            if (string.IsNullOrEmpty(value))
            {
                await js.InvokeVoidAsync("localStorage.removeItem", key);
            }
            else
            {
                await js.InvokeVoidAsync("localStorage.setItem", key, value);
            }
        }
        catch (JSException)
        {
        }
    }

    /// <summary>Удаляет значение из хранилища браузера.</summary>
    /// <param name="key">Ключ.</param>
    /// <returns>Задача завершения.</returns>
    private async Task RemoveAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (JSException)
        {
        }
    }
}
