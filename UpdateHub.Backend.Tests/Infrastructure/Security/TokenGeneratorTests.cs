using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using UpdateHub.BackendServer.Domain.Entities;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.BackendServer.Infrastructure.Security;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Backend.Tests.Infrastructure.Security;

/// <summary>
/// Проверяет выпуск access- и refresh-токенов.
/// </summary>
/// <remarks>
/// Состав утверждений в токене здесь не формальность: по ним работает
/// разграничение доступа. Роль читается из утверждения роли, и ошибка
/// в его имени открыла бы панель управления обычному пользователю.
/// </remarks>
public class TokenGeneratorTests
{
    /// <summary>Ключ длиной не меньше 32 байт, как требует проверка при старте.</summary>
    private const string TestSecret = "kluch-dlya-testov-dostatochno-dlinnyy-1234567890";

    /// <summary>Создаёт генератор с предсказуемыми настройками.</summary>
    /// <param name="accessMinutes">Срок жизни access-токена в минутах.</param>
    /// <param name="refreshDays">Срок жизни refresh-токена в сутках.</param>
    /// <returns>Готовый генератор.</returns>
    private static TokenGenerator CreateGenerator(int accessMinutes = 60, int refreshDays = 7)
        => new(Options.Create(new JwtSettings
        {
            Issuer = "UpdateHub",
            Audience = "UpdateClients",
            SecretKey = TestSecret,
            AccessTokenExpiryMinutes = accessMinutes,
            RefreshTokenExpiryDays = refreshDays
        }));

    /// <summary>Создаёт пользователя для выпуска токена.</summary>
    /// <param name="role">Роль пользователя.</param>
    /// <returns>Пользователь.</returns>
    private static UserEntity CreateUser(UserRole role = UserRole.Client)
        => new() { Id = "user-1", Username = "ivanov", Role = role };

    /// <summary>В токен попадают идентификатор, логин и роль пользователя.</summary>
    [Fact]
    public void GenerateAccessToken_ContainsIdentifierLoginAndRole()
    {
        var generator = CreateGenerator();
        var token = generator.GenerateAccessToken(CreateUser(UserRole.Admin));

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("user-1", parsed.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("ivanov", parsed.Claims.First(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("Admin", parsed.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    /// <summary>
    /// Роль обычного пользователя записывается как <c>Client</c>. Проверка
    /// парная к предыдущей: важно, что роль не «прилипает» к администратору.
    /// </summary>
    [Fact]
    public void GenerateAccessToken_RegularUser_GetsClientRole()
    {
        var generator = CreateGenerator();
        var token = generator.GenerateAccessToken(CreateUser(UserRole.Client));

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("Client", parsed.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    /// <summary>Издатель и получатель берутся из настроек.</summary>
    [Fact]
    public void GenerateAccessToken_IssuerAndAudience_TakenFromSettings()
    {
        var generator = CreateGenerator();
        var token = generator.GenerateAccessToken(CreateUser());

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("UpdateHub", parsed.Issuer);
        Assert.Contains("UpdateClients", parsed.Audiences);
    }

    /// <summary>
    /// Срок жизни берётся из настроек, а не из зашитого значения.
    /// Раньше он был захардкожен и расходился с конфигурацией.
    /// </summary>
    [Fact]
    public void GenerateAccessToken_Lifetime_TakenFromSettings()
    {
        var generator = CreateGenerator(accessMinutes: 15);
        var token = generator.GenerateAccessToken(CreateUser());

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var lifetime = parsed.ValidTo - DateTime.UtcNow;

        Assert.InRange(lifetime.TotalMinutes, 13, 16);
    }

    /// <summary>Свойства сроков жизни отражают настройки.</summary>
    [Fact]
    public void TokenLifetimes_MatchSettings()
    {
        var generator = CreateGenerator(accessMinutes: 45, refreshDays: 10);

        Assert.Equal(TimeSpan.FromMinutes(45), generator.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromDays(10), generator.RefreshTokenLifetime);
    }

    /// <summary>
    /// Каждый выпуск даёт новый идентификатор токена: по нему различаются
    /// два токена, выданных одному пользователю в одну секунду.
    /// </summary>
    [Fact]
    public void GenerateAccessToken_EachIssue_HasUniqueTokenId()
    {
        var generator = CreateGenerator();
        var user = CreateUser();

        var first = new JwtSecurityTokenHandler().ReadJwtToken(generator.GenerateAccessToken(user));
        var second = new JwtSecurityTokenHandler().ReadJwtToken(generator.GenerateAccessToken(user));

        Assert.NotEqual(
            first.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value,
            second.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
    }

    /// <summary>Refresh-токены не повторяются.</summary>
    [Fact]
    public void GenerateRefreshToken_ValuesAreUnique()
    {
        var generator = CreateGenerator();

        var tokens = Enumerable.Range(0, 100).Select(_ => generator.GenerateRefreshToken()).ToList();

        Assert.Equal(tokens.Count, tokens.Distinct().Count());
    }

    /// <summary>
    /// Refresh-токен не содержит символов, требующих экранирования в URL
    /// и в теле формы: скрипт передаёт его через <c>curl -d</c>.
    /// </summary>
    [Fact]
    public void GenerateRefreshToken_SafeForFormTransmission()
    {
        var generator = CreateGenerator();

        var token = generator.GenerateRefreshToken();

        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    /// <summary>Хэш одного и того же токена стабилен — по нему идёт поиск в базе.</summary>
    [Fact]
    public void HashRefreshToken_SameToken_ProducesStableHash()
    {
        var generator = CreateGenerator();
        var token = generator.GenerateRefreshToken();

        Assert.Equal(generator.HashRefreshToken(token), generator.HashRefreshToken(token));
    }

    /// <summary>Разные токены дают разные хэши.</summary>
    [Fact]
    public void HashRefreshToken_DifferentTokens_ProduceDifferentHashes()
    {
        var generator = CreateGenerator();

        var first = generator.HashRefreshToken(generator.GenerateRefreshToken());
        var second = generator.HashRefreshToken(generator.GenerateRefreshToken());

        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// Хэш не совпадает с самим токеном. В базе хранится только он, и её
    /// утечка не должна позволять обновить access-токен.
    /// </summary>
    [Fact]
    public void HashRefreshToken_DiffersFromOriginalToken()
    {
        var generator = CreateGenerator();
        var token = generator.GenerateRefreshToken();

        Assert.NotEqual(token, generator.HashRefreshToken(token));
    }
}
