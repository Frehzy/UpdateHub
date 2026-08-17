using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text;
using UpdateHub.Backend.Tests.TestSupport;

namespace UpdateHub.Backend.Tests.Api.V1.Controllers;

/// <summary>
/// Проверяет сравнение манифестов и выдачу эталонного манифеста.
/// </summary>
/// <remarks>
/// Это основной обмен: скрипт на компьютере отправляет вывод <c>md5sum</c>
/// и получает список того, что нужно скачать. Формат обеих сторон обязан
/// оставаться разбираемым обычными <c>while read</c> и <c>cut</c> — без jq,
/// которого на клиенте нет.
/// </remarks>
[Collection(ApiCollection.Name)]
public class SyncControllerTests(UpdateHubApplication application)
{
    /// <summary>Контрольная сумма строки «hello», посчитанная <c>md5sum</c>.</summary>
    private const string HelloMd5 = "5d41402abc4b2a76b9719d911017c592";

    /// <summary>Отправляет манифест компьютера на сравнение.</summary>
    /// <param name="client">Клиент с токеном.</param>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="manifest">Манифест в формате <c>md5sum</c>.</param>
    /// <returns>Ответ сервера.</returns>
    private static Task<HttpResponseMessage> PostDiffAsync(HttpClient client, string clientId, string manifest)
        => client.PostAsync(
            $"/api/v1/sync/diff?client_id={clientId}",
            new StringContent(manifest, Encoding.UTF8, "text/plain"));

    /// <summary>Готовит пользователя с правами на свой компьютер.</summary>
    /// <returns>Клиент с токеном и идентификатор компьютера.</returns>
    /// <remarks>
    /// Логин и идентификатор компьютера делаются уникальными: приложение общее
    /// на всю группу тестов, а логин в базе обязан быть единственным.
    /// </remarks>
    private async Task<(HttpClient Client, string ClientId)> CreateClientWithAccessAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var username = $"sync-{suffix}";
        var clientId = $"pc-sync-{suffix}";

        var user = await application.AddUserAsync(username, "parol12345");
        await application.AddClientAsync(clientId, user.Id);

        return (await application.CreateAuthorizedClientAsync(username, "parol12345"), clientId);
    }

    /// <summary>Без токена обмен невозможен.</summary>
    [Fact]
    public async Task Manifest_WithoutToken_Unauthorized()
    {
        using var client = application.CreateApiClient();

        var response = await client.GetAsync("/api/v1/sync/manifest?client_id=pc-1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Обращение от имени незаведённого компьютера — 404: администратор ещё
    /// не одобрил заявку, и скрипту нужно отличить это от отказа в правах.
    /// </summary>
    [Fact]
    public async Task Manifest_UnknownClient_NotFound()
    {
        var (client, _) = await CreateClientWithAccessAsync();

        using (client)
        {
            var response = await client.GetAsync("/api/v1/sync/manifest?client_id=pc-kotorogo-net");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.StartsWith("error=", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Компьютер, заведённый, но не выданный пользователю, — 403.
    /// </summary>
    /// <remarks>
    /// Ровно то разграничение, ради которого заводились права: пользователь
    /// обновляет свои машины и не видит чужие.
    /// </remarks>
    [Fact]
    public async Task Manifest_ClientWithoutGrant_Forbidden()
    {
        var (client, _) = await CreateClientWithAccessAsync();
        await application.AddClientAsync("pc-chuzhoy-nichey");

        using (client)
        {
            var response = await client.GetAsync("/api/v1/sync/manifest?client_id=pc-chuzhoy-nichey");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    /// <summary>
    /// Манифест приходит строками вида «сумма  путь» — тем же форматом,
    /// который выдаёт <c>md5sum</c> на клиенте.
    /// </summary>
    [Fact]
    public async Task Manifest_ReturnsMd5SumFormat()
    {
        await application.PublishFileAsync("docs/privet.txt", "hello");
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            var manifest = await client.GetStringAsync($"/api/v1/sync/manifest?client_id={clientId}");

            Assert.Contains($"{HelloMd5}  docs/privet.txt", manifest, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Пустой манифест компьютера означает «у меня нет ничего»: сервер
    /// возвращает состояние «update» и перечисляет файлы к загрузке.
    /// </summary>
    [Fact]
    public async Task Diff_EmptyClientManifest_PlansDownload()
    {
        await application.PublishFileAsync("docs/plan.txt", "hello");
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            var response = await PostDiffAsync(client, clientId, string.Empty);
            var plan = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("@STATUS update", plan, StringComparison.Ordinal);
            Assert.Contains($"{HelloMd5}  docs/plan.txt", plan, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Совпадающий манифест означает «обновлять нечего»: состояние «ok»
    /// и ноль файлов к загрузке.
    /// </summary>
    [Fact]
    public async Task Diff_MatchingManifest_PlansNothing()
    {
        await application.PublishFileAsync("docs/sovpadenie.txt", "hello");
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            // За эталон берётся сам ответ сервера: в каталоге лежат и файлы
            // от соседних тестов, перечислять их вручную нельзя.
            var manifest = await client.GetStringAsync($"/api/v1/sync/manifest?client_id={clientId}");

            var response = await PostDiffAsync(client, clientId, manifest);
            var plan = await response.Content.ReadAsStringAsync();

            Assert.Contains("@STATUS ok", plan, StringComparison.Ordinal);
            Assert.Contains("@COUNT 0", plan, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Устаревшая копия файла попадает в план: суммы не совпали.
    /// </summary>
    [Fact]
    public async Task Diff_ChangedFile_PlansRedownload()
    {
        await application.PublishFileAsync("docs/ustarelo.txt", "hello");
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            var manifest = "00000000000000000000000000000000  docs/ustarelo.txt\n";

            var response = await PostDiffAsync(client, clientId, manifest);
            var plan = await response.Content.ReadAsStringAsync();

            Assert.Contains($"{HelloMd5}  docs/ustarelo.txt", plan, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Лишний файл на компьютере отмечается строкой с восклицательным знаком.
    /// </summary>
    /// <remarks>
    /// Именно отмечается, а не удаляется: решение о судьбе лишних файлов
    /// принимает человек, сервер только сообщает о них.
    /// </remarks>
    [Fact]
    public async Task Diff_ExtraClientFile_ReportedButNotDeleted()
    {
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            var manifest = $"{HelloMd5}  starye/lishniy.txt\n";

            var response = await PostDiffAsync(client, clientId, manifest);
            var plan = await response.Content.ReadAsStringAsync();

            Assert.Contains("!starye/lishniy.txt", plan, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Испорченная строка манифеста не отменяет весь обмен: сервер отмечает
    /// её как замечание и продолжает разбор.
    /// </summary>
    [Fact]
    public async Task Diff_BrokenManifestLine_ReportedAsWarning()
    {
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            var response = await PostDiffAsync(client, clientId, "sovsem-ne-manifest\n");
            var plan = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("@WARN", plan, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Абсолютный путь в манифесте компьютера отклоняется.
    /// </summary>
    /// <remarks>
    /// Проверка защитная: приняв «/etc/passwd», сервер стал бы обсуждать
    /// с клиентом файлы вне каталога раздачи.
    /// </remarks>
    [Fact]
    public async Task Diff_AbsolutePathInManifest_Rejected()
    {
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            var response = await PostDiffAsync(client, clientId, $"{HelloMd5}  /etc/passwd\n");
            var plan = await response.Content.ReadAsStringAsync();

            // Замечание есть, а вот строки загрузки или отметки о лишнем файле
            // для этого пути быть не должно: путь отброшен, а не принят к сведению.
            Assert.Contains("@WARN", plan, StringComparison.Ordinal);
            Assert.DoesNotContain($"{HelloMd5}  /etc/passwd", plan, StringComparison.Ordinal);
            Assert.DoesNotContain("!/etc/passwd", plan, StringComparison.Ordinal);
        }
    }

    /// <summary>Запрос без указания компьютера отклоняется.</summary>
    [Fact]
    public async Task Diff_WithoutClientIdentifier_BadRequest()
    {
        var (client, _) = await CreateClientWithAccessAsync();

        using (client)
        {
            var response = await client.PostAsync(
                "/api/v1/sync/diff",
                new StringContent(string.Empty, Encoding.UTF8, "text/plain"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    /// <summary>
    /// Обмен записывается в журнал обращений: по нему администратор видит,
    /// какие компьютеры давно не выходили на связь.
    /// </summary>
    [Fact]
    public async Task Diff_RecordsRequestInJournal()
    {
        var (client, clientId) = await CreateClientWithAccessAsync();

        using (client)
        {
            await PostDiffAsync(client, clientId, string.Empty);
        }

        await application.WithDatabaseAsync(async context =>
        {
            var recorded = await context.UpdateRequests.AnyAsync(x => x.ClientId == clientId);

            Assert.True(recorded, "Обращение не попало в журнал");
        });
    }
}
