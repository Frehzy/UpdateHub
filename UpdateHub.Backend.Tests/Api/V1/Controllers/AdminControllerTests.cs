using System.Net.Http.Json;
using System.Text.Json;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Backend.Tests.Api.V1.Controllers;

/// <summary>
/// Проверяет панель управления.
/// </summary>
/// <remarks>
/// Главное здесь — разграничение по роли. Прежняя версия проверяла подпись
/// токена собственным обработчиком и нигде не сверяла роль, из-за чего любой
/// действующий токен открывал панель управления целиком: обычный пользователь
/// мог завести себе администратора. Теперь роль проверяет штатный механизм,
/// и первым делом проверяется именно отказ.
/// <para>
/// Панель, в отличие от клиентской части, отвечает JSON: её читает не bash,
/// а будущий веб-интерфейс.
/// </para>
/// </remarks>
[Collection(ApiCollection.Name)]
public class AdminControllerTests(UpdateHubApplication application)
{
    /// <summary>Создаёт клиента, вошедшего обычным пользователем.</summary>
    /// <returns>Клиент с токеном без роли администратора.</returns>
    private async Task<HttpClient> CreateOrdinaryUserClientAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = await application.AddUserAsync($"admin-test-{suffix}", "parol12345");
        await application.AddClientAsync($"pc-admin-test-{suffix}", user.Id);

        return await application.CreateAuthorizedClientAsync($"admin-test-{suffix}", "parol12345");
    }

    /// <summary>Панель недоступна без токена.</summary>
    [Fact]
    public async Task Users_WithoutToken_Unauthorized()
    {
        using var client = application.CreateApiClient();

        var response = await client.GetAsync("/api/v1/admin/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Обычному пользователю панель недоступна, хотя токен у него настоящий.
    /// </summary>
    /// <param name="path">Проверяемый адрес панели.</param>
    [Theory]
    [InlineData("/api/v1/admin/users")]
    [InlineData("/api/v1/admin/clients")]
    [InlineData("/api/v1/admin/groups")]
    [InlineData("/api/v1/admin/stats")]
    public async Task AdminEndpoints_OrdinaryUser_Forbidden(string path)
    {
        using var client = await CreateOrdinaryUserClientAsync();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Создание пользователя обычному пользователю тоже недоступно.</summary>
    [Fact]
    public async Task CreateUser_OrdinaryUser_Forbidden()
    {
        using var client = await CreateOrdinaryUserClientAsync();

        var response = await client.PostAsJsonAsync(
            "/api/v1/admin/users",
            new { username = "samozvanec", password = "parol12345", role = nameof(UserRole.Admin) });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>Администратор видит список пользователей.</summary>
    [Fact]
    public async Task Users_Administrator_ReturnsList()
    {
        using var client = await application.CreateAdminClientAsync();

        var response = await client.GetAsync("/api/v1/admin/users");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.GetProperty("total").GetInt32() > 0);
        Assert.Equal(JsonValueKind.Array, payload.GetProperty("users").ValueKind);
    }

    /// <summary>Панель отвечает JSON, а не текстом клиентской части.</summary>
    [Fact]
    public async Task Users_AnswersWithJson()
    {
        using var client = await application.CreateAdminClientAsync();

        var response = await client.GetAsync("/api/v1/admin/users");

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Группа заводится, читается по своему адресу и удаляется.</summary>
    [Fact]
    public async Task Groups_CreateReadDelete()
    {
        using var client = await application.CreateAdminClientAsync();
        var name = $"Бухгалтерия {Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync(
            "/api/v1/admin/groups",
            new { name, description = "Компьютеры бухгалтерии" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var group = await created.Content.ReadFromJsonAsync<JsonElement>();
        var groupId = group.GetProperty("id").GetString()!;

        var read = await client.GetAsync($"/api/v1/admin/groups/{groupId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        var deleted = await client.DeleteAsync($"/api/v1/admin/groups/{groupId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var readAgain = await client.GetAsync($"/api/v1/admin/groups/{groupId}");
        Assert.Equal(HttpStatusCode.NotFound, readAgain.StatusCode);
    }

    /// <summary>
    /// После удаления группы её название снова свободно.
    /// </summary>
    /// <remarks>
    /// Прежде удаление было мягким: запись оставалась в базе неактивной,
    /// уникальный индекс продолжал держать название занятым, а в списке групп
    /// администратор её не видел. Завести «Бухгалтерию» заново становилось
    /// невозможно, и понять почему — тоже.
    /// </remarks>
    [Fact]
    public async Task Groups_NameFreedAfterDeletion()
    {
        using var client = await application.CreateAdminClientAsync();
        var name = $"Отдел {Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync("/api/v1/admin/groups", new { name });
        var groupId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        await client.DeleteAsync($"/api/v1/admin/groups/{groupId}");

        var again = await client.PostAsJsonAsync("/api/v1/admin/groups", new { name });

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    /// <summary>
    /// Компьютер, заведённый в удалённой группе, остаётся без группы,
    /// а не исчезает вместе с ней.
    /// </summary>
    [Fact]
    public async Task Groups_DeletionLeavesClientsWithoutGroup()
    {
        using var client = await application.CreateAdminClientAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var created = await client.PostAsJsonAsync("/api/v1/admin/groups", new { name = $"Группа {suffix}" });
        var groupId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var clientId = $"pc-gruppa-{suffix}";
        await client.PostAsJsonAsync("/api/v1/admin/clients", new { clientId, groupId });

        await client.DeleteAsync($"/api/v1/admin/groups/{groupId}");

        var read = await client.GetAsync($"/api/v1/admin/clients/{clientId}");
        var payload = await read.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("groupId").ValueKind);
    }

    /// <summary>
    /// Удалённый компьютер заводится заново под тем же идентификатором.
    /// </summary>
    /// <remarks>
    /// Удаление компьютера мягкое, и это правильно: журнал обращений ссылается
    /// на него, и настоящее удаление стёрло бы историю. Но идентификатор задаёт
    /// сама машина, и после переустановки системы администратор одобряет заявку
    /// с тем же значением. Прежде это упиралось в «уже зарегистрирован»
    /// от записи, которой не видно ни в одном списке.
    /// </remarks>
    [Fact]
    public async Task Clients_DeletedClientCanBeRegisteredAgain()
    {
        using var client = await application.CreateAdminClientAsync();
        var clientId = $"pc-vozvrat-{Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync("/api/v1/admin/clients", new { clientId });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var deleted = await client.DeleteAsync($"/api/v1/admin/clients/{clientId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var again = await client.PostAsJsonAsync("/api/v1/admin/clients", new { clientId });
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);

        var list = await client.GetAsync($"/api/v1/admin/clients?search={clientId}");
        var payload = await list.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, payload.GetProperty("total").GetInt32());
    }

    /// <summary>
    /// Повторное название группы отклоняется: названия обязаны различаться,
    /// иначе выдавать по ним права бессмысленно.
    /// </summary>
    [Fact]
    public async Task Groups_DuplicateName_Rejected()
    {
        using var client = await application.CreateAdminClientAsync();
        var name = $"Склад {Guid.NewGuid():N}";

        var first = await client.PostAsJsonAsync("/api/v1/admin/groups", new { name });
        var second = await client.PostAsJsonAsync("/api/v1/admin/groups", new { name });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.NotEqual(HttpStatusCode.Created, second.StatusCode);
        Assert.True(
            (int)second.StatusCode is >= 400 and < 500,
            $"Ожидалась ошибка клиента, получено {(int)second.StatusCode}");
    }

    /// <summary>Группа без названия не заводится.</summary>
    [Fact]
    public async Task CreateGroup_WithoutName_BadRequest()
    {
        using var client = await application.CreateAdminClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/admin/groups", new { description = "Без названия" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Компьютер заводится и появляется в списке.</summary>
    [Fact]
    public async Task Clients_CreateAndList()
    {
        using var client = await application.CreateAdminClientAsync();
        var clientId = $"pc-panel-{Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync("/api/v1/admin/clients", new { clientId });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var list = await client.GetAsync($"/api/v1/admin/clients?search={clientId}");
        var payload = await list.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(1, payload.GetProperty("total").GetInt32());
    }

    /// <summary>Запрос несуществующего компьютера — 404 с сообщением в JSON.</summary>
    [Fact]
    public async Task Client_Unknown_NotFoundAsJson()
    {
        using var client = await application.CreateAdminClientAsync();

        var response = await client.GetAsync("/api/v1/admin/clients/pc-kotorogo-net");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// Заблокированный компьютер перестаёт получать обновления,
    /// а разблокировка возвращает доступ.
    /// </summary>
    [Fact]
    public async Task BlockClient_StopsSynchronizationUntilUnblocked()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = await application.AddUserAsync($"blokirovka-{suffix}", "parol12345");
        var clientId = $"pc-blokirovka-{suffix}";
        await application.AddClientAsync(clientId, user.Id);

        using var admin = await application.CreateAdminClientAsync();
        using var owner = await application.CreateAuthorizedClientAsync($"blokirovka-{suffix}", "parol12345");

        var blocked = await admin.PostAsJsonAsync(
            $"/api/v1/admin/clients/{clientId}/block",
            new { reason = "Компьютер выведен из эксплуатации" });
        Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);

        var afterBlock = await owner.GetAsync($"/api/v1/sync/manifest?client_id={clientId}");
        Assert.Equal(HttpStatusCode.Forbidden, afterBlock.StatusCode);

        var unblocked = await admin.PostAsync($"/api/v1/admin/clients/{clientId}/unblock", content: null);
        Assert.Equal(HttpStatusCode.OK, unblocked.StatusCode);

        var afterUnblock = await owner.GetAsync($"/api/v1/sync/manifest?client_id={clientId}");
        Assert.Equal(HttpStatusCode.OK, afterUnblock.StatusCode);
    }

    /// <summary>
    /// Выданное право на компьютер открывает обмен, отозванное — закрывает.
    /// </summary>
    [Fact]
    public async Task ClientAccess_GrantAndRevoke()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = await application.AddUserAsync($"prava-{suffix}", "parol12345");
        var ownClientId = $"pc-prava-svoy-{suffix}";
        var grantedClientId = $"pc-prava-vydannyy-{suffix}";

        // Один компьютер нужен сразу: без единого права пользователь не войдёт.
        await application.AddClientAsync(ownClientId, user.Id);
        await application.AddClientAsync(grantedClientId);

        using var admin = await application.CreateAdminClientAsync();
        using var owner = await application.CreateAuthorizedClientAsync($"prava-{suffix}", "parol12345");

        var beforeGrant = await owner.GetAsync($"/api/v1/sync/manifest?client_id={grantedClientId}");
        Assert.Equal(HttpStatusCode.Forbidden, beforeGrant.StatusCode);

        var granted = await admin.PutAsync($"/api/v1/admin/users/{user.Id}/clients/{grantedClientId}", content: null);
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);

        var afterGrant = await owner.GetAsync($"/api/v1/sync/manifest?client_id={grantedClientId}");
        Assert.Equal(HttpStatusCode.OK, afterGrant.StatusCode);

        var revoked = await admin.DeleteAsync($"/api/v1/admin/users/{user.Id}/clients/{grantedClientId}");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode);

        var afterRevoke = await owner.GetAsync($"/api/v1/sync/manifest?client_id={grantedClientId}");
        Assert.Equal(HttpStatusCode.Forbidden, afterRevoke.StatusCode);
    }

    /// <summary>
    /// Отключение учётной записи отзывает выданные ей refresh-токены.
    /// </summary>
    /// <remarks>
    /// Иначе отключённый пользователь продолжал бы обновлять access-токен
    /// и работать до истечения срока refresh-токена — то есть неделю.
    /// </remarks>
    [Fact]
    public async Task DisableUser_RevokesIssuedRefreshTokens()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = await application.AddUserAsync($"otklyuchenie-{suffix}", "parol12345");
        await application.AddClientAsync($"pc-otklyuchenie-{suffix}", user.Id);

        using var client = application.CreateApiClient();

        var login = await client.PostAsync("/api/v1/auth/login", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("username", $"otklyuchenie-{suffix}"),
            new KeyValuePair<string, string>("password", "parol12345")
        ]));

        var tokens = UpdateHubApplication.ParseTextPairs(await login.Content.ReadAsStringAsync());

        using var admin = await application.CreateAdminClientAsync();
        var disabled = await admin.PutAsJsonAsync($"/api/v1/admin/users/{user.Id}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);

        var refresh = await client.PostAsync("/api/v1/auth/refresh", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("refresh_token", tokens["refresh_token"])
        ]));

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    /// <summary>Состояние манифеста доступно администратору.</summary>
    [Fact]
    public async Task ManifestStatus_ReturnsGenerationAndCount()
    {
        await application.PublishFileAsync("docs/sostoyanie.txt", "hello");

        using var client = await application.CreateAdminClientAsync();

        var response = await client.GetAsync("/api/v1/admin/manifest/status");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.GetProperty("entryCount").GetInt32() > 0);
        Assert.False(payload.GetProperty("isScanning").GetBoolean());
    }

    /// <summary>Внеочередной обход каталога выполняется по требованию.</summary>
    /// <remarks>
    /// Обход по таймеру идёт раз в минуту, и ждать его после подкладывания
    /// файлов администратору незачем — на то и кнопка.
    /// </remarks>
    [Fact]
    public async Task RescanManifest_RunsImmediately()
    {
        using var client = await application.CreateAdminClientAsync();

        var response = await client.PostAsync("/api/v1/admin/manifest/rescan", content: null);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", payload.GetProperty("status").GetString());
    }

    /// <summary>Статистика отдаётся и не требует параметров.</summary>
    [Fact]
    public async Task Stats_Administrator_ReturnsData()
    {
        using var client = await application.CreateAdminClientAsync();

        var response = await client.GetAsync("/api/v1/admin/stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>Заявки на регистрацию видны администратору.</summary>
    [Fact]
    public async Task Enrollments_Administrator_SeesSubmittedRequest()
    {
        using var anonymous = application.CreateApiClient();
        var clientId = $"pc-zayavka-panel-{Guid.NewGuid():N}";

        await anonymous.PostAsync("/api/v1/enroll", new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("client_id", clientId)
        ]));

        using var client = await application.CreateAdminClientAsync();

        var response = await client.GetAsync("/api/v1/admin/enrollments");
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(clientId, text, StringComparison.Ordinal);
    }
}
