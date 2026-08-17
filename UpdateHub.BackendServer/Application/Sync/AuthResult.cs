namespace UpdateHub.BackendServer.Application.Sync;

/// <summary>
/// Итог успешного входа или обновления токенов.
/// </summary>
/// <param name="AccessToken">Access-токен для заголовка Authorization.</param>
/// <param name="RefreshToken">Refresh-токен для последующего обновления.</param>
/// <param name="ExpiresInSeconds">Срок жизни access-токена в секундах.</param>
/// <param name="UserId">Идентификатор пользователя.</param>
/// <param name="Username">Логин пользователя.</param>
/// <param name="Role">Роль пользователя.</param>
/// <param name="ClientId">Компьютер, для которого выдан токен.</param>
/// <param name="MustChangePassword">Требуется ли смена пароля.</param>
public sealed record AuthResult(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    string UserId,
    string Username,
    string Role,
    string? ClientId,
    bool MustChangePassword);
