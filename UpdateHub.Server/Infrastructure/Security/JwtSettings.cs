namespace UpdateHub.Server.Infrastructure.Security;

/// <summary>
/// Параметры выпуска и проверки JWT. Читаются из секции <c>Jwt</c>.
/// </summary>
public class JwtSettings
{
    /// <summary>Издатель токена, попадает в claim <c>iss</c>.</summary>
    public string Issuer { get; set; } = "UpdateHub";

    /// <summary>Получатель токена, попадает в claim <c>aud</c>.</summary>
    public string Audience { get; set; } = "UpdateClients";

    /// <summary>
    /// Ключ подписи HMAC-SHA256. Не должен храниться в репозитории —
    /// задаётся переменной окружения <c>Jwt__SecretKey</c>.
    /// Минимальная длина — 32 байта, проверяется при старте.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Срок жизни access-токена в минутах.</summary>
    public int AccessTokenExpiryMinutes { get; set; } = 60;

    /// <summary>Срок жизни refresh-токена в сутках.</summary>
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
