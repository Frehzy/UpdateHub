using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Application.Sync;

/// <summary>
/// Запрос на сравнение манифестов.
/// </summary>
/// <param name="ClientId">Компьютер, за которым работает пользователь.</param>
/// <param name="Username">Логин пользователя.</param>
/// <param name="RequestType">Тип обращения: только сверка или подготовка к скачиванию.</param>
/// <param name="ClientManifest">Манифест клиента: путь — контрольная сумма.</param>
public sealed record SyncRequest(
    string ClientId,
    string? Username,
    RequestType RequestType,
    IReadOnlyDictionary<string, string> ClientManifest);
