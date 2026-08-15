using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Security;

namespace UpdateHub.Server.Application.Services;

public class TokenService(
    TokenGenerator tokenGenerator,
    IRefreshTokenRepository refreshTokenRepository,
    IUserRepository userRepository) : ITokenService
{
    public string GenerateAccessToken(UserEntity user)
    {
        return tokenGenerator.GenerateAccessToken(user);
    }

    public string GenerateRefreshToken()
    {
        return tokenGenerator.GenerateRefreshToken();
    }

    public string HashRefreshToken(string refreshToken)
    {
        return tokenGenerator.HashRefreshToken(refreshToken);
    }

    public (bool IsValid, string? UserId, string? Role, string? Username) ValidateAccessToken(string token)
    {
        var (isValid, principal) = tokenGenerator.ValidateAccessToken(token);
        if (!isValid || principal == null)
        {
            return (false, null, null, null);
        }

        var userId = principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var role = principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var username = principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;

        return (true, userId, role, username);
    }

    public async Task<bool> ValidateRefreshTokenAsync(string refreshToken, string userId)
    {
        var hashedToken = tokenGenerator.HashRefreshToken(refreshToken);
        var tokenEntity = await refreshTokenRepository.GetByTokenAsync(hashedToken);

        if (tokenEntity == null || tokenEntity.UserId != userId)
        {
            return false;
        }

        if (tokenEntity.ExpiresAt < DateTime.UtcNow || tokenEntity.RevokedAt != null)
        {
            return false;
        }

        return true;
    }

    public async Task<RefreshTokenEntity> StoreRefreshTokenAsync(string userId, string refreshToken, string? clientIp, string? userAgent)
    {
        var entity = new RefreshTokenEntity
        {
            UserId = userId,
            Token = tokenGenerator.HashRefreshToken(refreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            ClientIp = clientIp,
            UserAgent = userAgent
        };

        return await refreshTokenRepository.CreateAsync(entity);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        var hashedToken = tokenGenerator.HashRefreshToken(refreshToken);
        var tokenEntity = await refreshTokenRepository.GetByTokenAsync(hashedToken);

        if (tokenEntity != null)
        {
            tokenEntity.RevokedAt = DateTime.UtcNow;
            await refreshTokenRepository.UpdateAsync(tokenEntity);
        }
    }
}