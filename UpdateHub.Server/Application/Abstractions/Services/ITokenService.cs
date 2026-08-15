using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Services;

public interface ITokenService
{
    string GenerateAccessToken(UserEntity user);
    string GenerateRefreshToken();
    string HashRefreshToken(string refreshToken);
    (bool IsValid, string? UserId, string? Role, string? Username) ValidateAccessToken(string token);
    Task<bool> ValidateRefreshTokenAsync(string refreshToken, string userId);
    Task<RefreshTokenEntity> StoreRefreshTokenAsync(string userId, string refreshToken, string? clientIp, string? userAgent);
    Task RevokeRefreshTokenAsync(string refreshToken);
}