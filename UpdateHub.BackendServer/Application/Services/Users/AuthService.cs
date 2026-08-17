using UpdateHub.BackendServer.Application.Abstractions.Repositories.Users;
using UpdateHub.BackendServer.Application.Abstractions.Repositories;
using UpdateHub.BackendServer.Application.Abstractions.Services.Clients;
using UpdateHub.BackendServer.Application.Abstractions.Services.Users;
using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Domain.Entities.Users;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.BackendServer.Infrastructure.Security;
using UpdateHub.Shared.Enums;

namespace UpdateHub.BackendServer.Application.Services.Users;

/// <summary>Вход в систему, обновление и отзыв токенов, управление паролями.</summary>
/// <param name="userRepository">Доступ к учётным записям.</param>
/// <param name="refreshTokenRepository">Доступ к refresh-токенам.</param>
/// <param name="userClientAccessRepository">Доступ к персональным разрешениям.</param>
/// <param name="userGroupAccessRepository">Доступ к разрешениям на группы.</param>
/// <param name="clientAccessService">Проверка прав на компьютер.</param>
/// <param name="clientService">Управление компьютерами.</param>
/// <param name="tokenGenerator">Выпуск токенов.</param>
/// <param name="passwordHasher">Хэширование паролей.</param>
/// <param name="logger">Журнал.</param>
public class AuthService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUserClientAccessRepository userClientAccessRepository,
    IUserGroupAccessRepository userGroupAccessRepository,
    IClientAccessService clientAccessService,
    IClientService clientService,
    TokenGenerator tokenGenerator,
    PasswordHasher passwordHasher,
    ILogger<AuthService> logger) : IAuthService
{
    /// <summary>Минимальная длина пароля.</summary>
    private const int MinPasswordLength = 8;

    /// <inheritdoc />
    public async Task<AuthResult> LoginAsync(
        string username,
        string password,
        string clientId,
        ConnectionContext context,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByUsernameAsync(username, cancellationToken);

        // Пароль проверяется даже при отсутствии пользователя, чтобы время ответа
        // не позволяло отличить неизвестный логин от неверного пароля.
        var passwordValid = user is not null && passwordHasher.VerifyPassword(password, user.PasswordHash);

        if (user is null || !passwordValid)
        {
            logger.LogWarning("Неудачный вход под логином {Username} с адреса {Ip}", username, context.RemoteIpAddress);
            throw new AuthenticationFailedException("Неверный логин или пароль");
        }

        if (!user.IsActive)
        {
            throw new AuthenticationFailedException("Учётная запись отключена");
        }

        var isAdmin = user.Role == UserRole.Admin;

        // Вход без указания компьютера — режим панели управления. Он обязателен,
        // иначе система неразрешима: на пустой базе нет ни одного компьютера,
        // и администратор, созданный при первом запуске, не смог бы войти,
        // чтобы завести первый компьютер или рассмотреть заявку на регистрацию.
        if (string.IsNullOrWhiteSpace(clientId))
        {
            // Обычному пользователю такой токен выдаётся только если ему выданы
            // права хоть на что-то: иначе он всё равно бесполезен.
            if (!isAdmin && !await clientAccessService.HasAnyAccessAsync(user.Id, cancellationToken))
            {
                throw new AuthenticationFailedException(
                    "У вас нет прав ни на один компьютер. Обратитесь к администратору");
            }

            user.LastLogin = DateTime.UtcNow;
            await userRepository.UpdateAsync(user, cancellationToken);

            logger.LogInformation("Пользователь {Username} вошёл без привязки к компьютеру", user.Username);
            return await IssueTokensAsync(user, null, context, cancellationToken);
        }

        // Проверка компьютера идёт до любых изменений в базе: неизвестный
        // идентификатор не должен приводить к появлению новой записи о клиенте.
        var access = await clientAccessService.AuthorizeAsync(user.Id, isAdmin, clientId, cancellationToken);
        if (!access.IsAllowed)
        {
            throw new AuthenticationFailedException(access.Reason ?? "Доступ к компьютеру запрещён");
        }

        user.LastLogin = DateTime.UtcNow;
        await userRepository.UpdateAsync(user, cancellationToken);

        await clientService.AddHistoryAsync(
            clientId,
            ClientChangeType.LoggedIn,
            null,
            $"{user.Username} с адреса {context.RemoteIpAddress ?? "неизвестно"}",
            cancellationToken);

        var result = await IssueTokensAsync(user, clientId, context, cancellationToken);

        logger.LogInformation("Пользователь {Username} вошёл на компьютере {ClientId}", user.Username, clientId);
        return result;
    }

    /// <inheritdoc />
    public async Task<AuthResult> RefreshAsync(
        string refreshToken,
        ConnectionContext context,
        CancellationToken cancellationToken = default)
    {
        var hash = tokenGenerator.HashRefreshToken(refreshToken);
        var stored = await refreshTokenRepository.GetByHashAsync(hash, cancellationToken);

        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt < DateTime.UtcNow)
        {
            throw new AuthenticationFailedException("Refresh-токен недействителен или истёк");
        }

        var user = await userRepository.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new AuthenticationFailedException("Учётная запись не найдена или отключена");
        }

        // Ротация: старый токен отзывается сразу, чтобы перехваченным значением
        // нельзя было воспользоваться после законного владельца.
        await refreshTokenRepository.RevokeAsync(hash, cancellationToken);

        return await IssueTokensAsync(user, null, context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task LogoutAsync(string refreshToken, string userId, CancellationToken cancellationToken = default)
    {
        var hash = tokenGenerator.HashRefreshToken(refreshToken);
        var stored = await refreshTokenRepository.GetByHashAsync(hash, cancellationToken);

        // Чужой токен отозвать нельзя: иначе, зная значение, можно было бы
        // разлогинить другого пользователя.
        if (stored is null || stored.UserId != userId || stored.RevokedAt is not null)
        {
            return;
        }

        await refreshTokenRepository.RevokeAsync(hash, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new AuthenticationFailedException("Пользователь не найден");

        if (!passwordHasher.VerifyPassword(currentPassword, user.PasswordHash))
        {
            throw new AuthenticationFailedException("Текущий пароль указан неверно");
        }

        ValidatePassword(newPassword);

        if (passwordHasher.VerifyPassword(newPassword, user.PasswordHash))
        {
            throw new ArgumentException("Новый пароль должен отличаться от текущего");
        }

        user.PasswordHash = passwordHasher.HashPassword(newPassword);
        user.MustChangePassword = false;
        await userRepository.UpdateAsync(user, cancellationToken);

        // Смена пароля обесценивает все ранее выданные refresh-токены.
        var revoked = await refreshTokenRepository.RevokeAllForUserAsync(userId, cancellationToken);
        logger.LogInformation("Пользователь {Username} сменил пароль, отозвано токенов: {Count}", user.Username, revoked);
    }

    /// <inheritdoc />
    public async Task<UserEntity> CreateUserAsync(
        string username,
        string password,
        UserRole role,
        IReadOnlyCollection<string>? groupIds,
        IReadOnlyCollection<string>? clientIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Логин не может быть пустым");
        }

        ValidatePassword(password);

        if (await userRepository.GetByUsernameAsync(username, cancellationToken) is not null)
        {
            throw new InvalidOperationException($"Логин '{username}' уже занят");
        }

        var user = new UserEntity
        {
            Username = username,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = role,
            IsActive = true,
            MustChangePassword = true
        };

        await userRepository.CreateAsync(user, cancellationToken);

        foreach (var groupId in groupIds ?? [])
        {
            await userGroupAccessRepository.CreateAsync(
                new UserGroupAccessEntity { UserId = user.Id, GroupId = groupId },
                cancellationToken);
        }

        foreach (var clientId in clientIds ?? [])
        {
            await userClientAccessRepository.CreateAsync(
                new UserClientAccessEntity { UserId = user.Id, ClientId = clientId },
                cancellationToken);
        }

        logger.LogInformation("Создан пользователь {Username} с ролью {Role}", user.Username, role);
        return user;
    }

    /// <summary>
    /// Выпускает пару токенов и сохраняет хэш refresh-токена.
    /// </summary>
    /// <param name="user">Пользователь.</param>
    /// <param name="clientId">Компьютер, если вход выполнен с него.</param>
    /// <param name="context">Сведения о соединении.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Выданные токены.</returns>
    private async Task<AuthResult> IssueTokensAsync(
        UserEntity user,
        string? clientId,
        ConnectionContext context,
        CancellationToken cancellationToken)
    {
        var accessToken = tokenGenerator.GenerateAccessToken(user);
        var refreshToken = tokenGenerator.GenerateRefreshToken();

        await refreshTokenRepository.CreateAsync(new RefreshTokenEntity
        {
            UserId = user.Id,
            Token = tokenGenerator.HashRefreshToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.Add(tokenGenerator.RefreshTokenLifetime),
            ClientIp = context.RemoteIpAddress,
            UserAgent = context.UserAgent
        }, cancellationToken);

        return new AuthResult(
            accessToken,
            refreshToken,
            (int)tokenGenerator.AccessTokenLifetime.TotalSeconds,
            user.Id,
            user.Username,
            user.Role.ToString(),
            clientId,
            user.MustChangePassword);
    }

    /// <summary>
    /// Проверяет пароль на соответствие минимальным требованиям.
    /// </summary>
    /// <param name="password">Проверяемый пароль.</param>
    /// <exception cref="ArgumentException">Пароль слишком короткий или пустой.</exception>
    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
        {
            throw new ArgumentException($"Пароль должен содержать не менее {MinPasswordLength} символов");
        }
    }
}
