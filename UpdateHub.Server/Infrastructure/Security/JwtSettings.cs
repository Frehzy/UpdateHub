namespace UpdateHub.Server.Infrastructure.Security;

public class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenExpiryMinutes { get; set; } = 1440;
    public int RefreshTokenExpiryDays { get; set; } = 7;
}