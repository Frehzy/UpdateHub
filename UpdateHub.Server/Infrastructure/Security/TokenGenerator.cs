using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Infrastructure.Security;

/// <summary>
/// Выпуск access-токенов и refresh-токенов.
/// </summary>
/// <param name="jwtSettings">Параметры выпуска токенов.</param>
public class TokenGenerator(IOptions<JwtSettings> jwtSettings)
{
    private readonly JwtSettings _settings = jwtSettings.Value;

    /// <summary>Срок жизни access-токена.</summary>
    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(_settings.AccessTokenExpiryMinutes);

    /// <summary>Срок жизни refresh-токена.</summary>
    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_settings.RefreshTokenExpiryDays);

    /// <summary>
    /// Выпускает подписанный access-токен для пользователя.
    /// </summary>
    /// <param name="user">Пользователь, которому выдаётся токен.</param>
    /// <returns>Строковое представление JWT.</returns>
    public string GenerateAccessToken(UserEntity user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(AccessTokenLifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Генерирует случайный refresh-токен.
    /// </summary>
    /// <returns>Токен в кодировке Base64 без символов, требующих экранирования в URL.</returns>
    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    /// Вычисляет хэш refresh-токена для хранения в базе.
    /// </summary>
    /// <param name="refreshToken">Значение токена.</param>
    /// <returns>SHA-256 в кодировке Base64.</returns>
    /// <remarks>
    /// В базе хранится только хэш, поэтому её утечка не позволяет обновить access-токен.
    /// </remarks>
    public string HashRefreshToken(string refreshToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
        return Convert.ToBase64String(bytes);
    }
}
