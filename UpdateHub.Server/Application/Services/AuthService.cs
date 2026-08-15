using AutoMapper;
using UpdateHub.Server.Api.V1.DTOs.Request;
using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Domain.Enums;
using UpdateHub.Server.Infrastructure.Security;

namespace UpdateHub.Server.Application.Services;

public class AuthService(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IUserClientAccessRepository userClientAccessRepository,
    IUserGroupAccessRepository userGroupAccessRepository,
    IClientService clientService,
    TokenGenerator tokenGenerator,
    PasswordHasher passwordHasher,
    IMapper mapper,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponseDto> LoginAsync(AuthRequestDto request, string? userAgent)
    {
        var user = await userRepository.GetByUsernameAsync(request.Username);
        if (user == null || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid username or password");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("User account is disabled");
        }

        // Проверяем, есть ли у пользователя доступ к чему-либо
        var hasAccess = await HasAccessToAnyClientAsync(user.Id);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("User has no access to any clients or groups");
        }

        // Обновляем информацию о клиенте
        var client = await clientService.GetOrCreateClientAsync(request.ClientInfo!);

        // Проверяем доступ к этому клиенту
        if (!await HasAccessToClientAsync(user.Id, client.Id))
        {
            throw new UnauthorizedAccessException("Access denied to this client");
        }

        // Обновляем время последнего входа
        user.LastLogin = DateTime.UtcNow;
        await userRepository.UpdateAsync(user);

        // Генерируем токены
        var accessToken = tokenGenerator.GenerateAccessToken(user);
        var refreshToken = tokenGenerator.GenerateRefreshToken();

        // Сохраняем refresh token
        var refreshTokenEntity = await refreshTokenRepository.CreateAsync(new RefreshTokenEntity
        {
            UserId = user.Id,
            Token = tokenGenerator.HashRefreshToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            ClientIp = request.ClientInfo?.IpAddress,
            UserAgent = userAgent
        });

        // Создаём сессию
        await clientService.AddClientHistoryAsync(
            client.Id,
            ClientChangeType.SessionCreated.ToString(),
            null,
            $"Login from {request.ClientInfo?.IpAddress}",
            null);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = 86400,
            UserId = user.Id,
            Username = user.Username,
            Role = user.Role.ToString(),
            ClientId = client.Id,
            MustChangePassword = user.MustChangePassword
        };
    }

    public async Task<RefreshResponseDto> RefreshTokenAsync(string refreshToken)
    {
        var hashedToken = tokenGenerator.HashRefreshToken(refreshToken);
        var tokenEntity = await refreshTokenRepository.GetByTokenAsync(hashedToken);

        if (tokenEntity == null || tokenEntity.ExpiresAt < DateTime.UtcNow || tokenEntity.RevokedAt != null)
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        var user = await userRepository.GetByIdAsync(tokenEntity.UserId);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("User not found or disabled");
        }

        var accessToken = tokenGenerator.GenerateAccessToken(user);

        return new RefreshResponseDto
        {
            AccessToken = accessToken,
            ExpiresIn = 86400
        };
    }

    public async Task LogoutAsync(string refreshToken, string userId)
    {
        var hashedToken = tokenGenerator.HashRefreshToken(refreshToken);
        var tokenEntity = await refreshTokenRepository.GetByTokenAsync(hashedToken);

        if (tokenEntity != null && tokenEntity.UserId == userId)
        {
            tokenEntity.RevokedAt = DateTime.UtcNow;
            await refreshTokenRepository.UpdateAsync(tokenEntity);
        }
    }

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await userRepository.GetByIdAsync(userId) ?? throw new ArgumentException("User not found");
        if (!passwordHasher.VerifyPassword(currentPassword, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Current password is incorrect");
        }

        user.PasswordHash = passwordHasher.HashPassword(newPassword);
        user.MustChangePassword = false;
        await userRepository.UpdateAsync(user);
    }

    public async Task<UserEntity> CreateUserAsync(string username, string password, string role, List<string>? groupIds, List<string>? clientIds)
    {
        if (await userRepository.GetByUsernameAsync(username) != null)
        {
            throw new InvalidOperationException("Username already exists");
        }

        var user = new UserEntity
        {
            Username = username,
            PasswordHash = passwordHasher.HashPassword(password),
            Role = Enum.Parse<UserRole>(role, true),
            IsActive = true,
            MustChangePassword = false
        };

        user = await userRepository.CreateAsync(user);

        // Добавляем доступ к группам
        if (groupIds != null)
        {
            foreach (var groupId in groupIds)
            {
                await userGroupAccessRepository.CreateAsync(new UserGroupAccessEntity
                {
                    UserId = user.Id,
                    GroupId = groupId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Добавляем доступ к конкретным клиентам
        if (clientIds != null)
        {
            foreach (var clientId in clientIds)
            {
                await userClientAccessRepository.CreateAsync(new UserClientAccessEntity
                {
                    UserId = user.Id,
                    ClientId = clientId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        return user;
    }

    public async Task<bool> HasAccessToClientAsync(string userId, string clientId)
    {
        // Проверяем прямой доступ к клиенту
        var access = await userClientAccessRepository.GetByUserAndClientAsync(userId, clientId);
        if (access != null)
        {
            return true;
        }

        // Проверяем доступ через группы
        var client = await clientService.GetClientByIdAsync(clientId);
        if (client?.GroupId != null)
        {
            var groupAccess = await userGroupAccessRepository.GetByUserAndGroupAsync(userId, client.GroupId);
            if (groupAccess != null)
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> HasAccessToAnyClientAsync(string userId)
    {
        // Проверяем прямой доступ к клиентам
        var clientAccesses = await userClientAccessRepository.GetByUserIdAsync(userId);
        if (clientAccesses.Any())
        {
            return true;
        }

        // Проверяем доступ через группы
        var groupAccesses = await userGroupAccessRepository.GetByUserIdAsync(userId);
        if (groupAccesses.Any())
        {
            return true;
        }

        return false;
    }

    public async Task<IEnumerable<string>> GetUserClientIdsAsync(string userId)
    {
        var clientAccesses = await userClientAccessRepository.GetByUserIdAsync(userId);
        return clientAccesses.Select(a => a.ClientId);
    }

    public async Task<IEnumerable<string>> GetUserGroupIdsAsync(string userId)
    {
        var groupAccesses = await userGroupAccessRepository.GetByUserIdAsync(userId);
        return groupAccesses.Select(a => a.GroupId);
    }
}