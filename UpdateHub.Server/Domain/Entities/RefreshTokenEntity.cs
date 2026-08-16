namespace UpdateHub.Server.Domain.Entities;

/// <summary>
/// Выданный refresh-токен. В базе хранится только SHA-256 от самого токена,
/// поэтому утечка базы не позволяет выпустить access-токен.
/// </summary>
public class RefreshTokenEntity
{
    /// <summary>Первичный ключ.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Владелец токена.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>SHA-256 от значения токена в кодировке Base64.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Момент истечения срока действия.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Момент выпуска.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Момент отзыва. Заполняется при выходе, смене пароля, отключении
    /// учётной записи и при ротации токена во время обновления.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>IP-адрес, с которого выпущен токен (берётся из соединения).</summary>
    public string? ClientIp { get; set; }

    /// <summary>Значение заголовка User-Agent на момент выпуска.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Владелец токена (навигационное свойство).</summary>
    public UserEntity? User { get; set; }
}
