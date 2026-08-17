using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Domain.Entities.Users;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.Shared.Enums;

namespace UpdateHub.BackendServer.Application.Abstractions.Services.Users;

/// <summary>Вход в систему, обновление и отзыв токенов, управление паролями.</summary>
public interface IAuthService
{
    /// <summary>
    /// Проверяет учётные данные и выдаёт пару токенов.
    /// </summary>
    /// <param name="username">Логин.</param>
    /// <param name="password">Пароль.</param>
    /// <param name="clientId">Компьютер, за которым работает пользователь.</param>
    /// <param name="context">Сведения о соединении.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Выданные токены и сведения о пользователе.</returns>
    /// <exception cref="AuthenticationFailedException">Учётные данные неверны либо доступ запрещён.</exception>
    Task<AuthResult> LoginAsync(
        string username,
        string password,
        string clientId,
        ConnectionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Обменивает refresh-токен на новую пару токенов.
    /// </summary>
    /// <param name="refreshToken">Действующий refresh-токен.</param>
    /// <param name="context">Сведения о соединении.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Новая пара токенов.</returns>
    /// <exception cref="AuthenticationFailedException">Токен недействителен либо отозван.</exception>
    /// <remarks>
    /// Прежний токен отзывается: ротация не позволяет пользоваться перехваченным
    /// значением после того, как им воспользовался законный владелец.
    /// </remarks>
    Task<AuthResult> RefreshAsync(
        string refreshToken,
        ConnectionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>Отзывает refresh-токен.</summary>
    /// <param name="refreshToken">Отзываемый токен.</param>
    /// <param name="userId">Идентификатор владельца.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task LogoutAsync(string refreshToken, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Меняет пароль и отзывает все выданные пользователю refresh-токены.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="currentPassword">Текущий пароль.</param>
    /// <param name="newPassword">Новый пароль.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <exception cref="AuthenticationFailedException">Текущий пароль неверен.</exception>
    /// <exception cref="ArgumentException">Новый пароль не удовлетворяет требованиям.</exception>
    Task ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    /// <summary>Создаёт учётную запись.</summary>
    /// <param name="username">Логин.</param>
    /// <param name="password">Начальный пароль.</param>
    /// <param name="role">Роль.</param>
    /// <param name="groupIds">Группы, к которым сразу выдаётся доступ.</param>
    /// <param name="clientIds">Компьютеры, к которым сразу выдаётся доступ.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданный пользователь.</returns>
    /// <exception cref="InvalidOperationException">Логин уже занят.</exception>
    Task<UserEntity> CreateUserAsync(
        string username,
        string password,
        UserRole role,
        IReadOnlyCollection<string>? groupIds,
        IReadOnlyCollection<string>? clientIds,
        CancellationToken cancellationToken = default);
}
