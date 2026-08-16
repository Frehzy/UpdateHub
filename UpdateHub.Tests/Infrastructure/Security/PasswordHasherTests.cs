using UpdateHub.Server.Infrastructure.Security;

namespace UpdateHub.Tests.Infrastructure.Security;

/// <summary>
/// Проверяет хэширование паролей.
/// </summary>
/// <remarks>
/// Стоимость вычисления здесь намеренно занижена: с боевым значением 12
/// каждая проверка занимает около трети секунды, и десяток тестов
/// превратился бы в несколько секунд ожидания. Проверяемое поведение
/// от стоимости не зависит.
/// </remarks>
public class PasswordHasherTests
{
    /// <summary>Создаёт хэшер с заниженной стоимостью, чтобы тесты шли быстро.</summary>
    /// <returns>Готовый хэшер.</returns>
    private static PasswordHasher CreateHasher() => new(workFactor: 4);

    /// <summary>Правильный пароль проходит проверку.</summary>
    [Fact]
    public void VerifyPassword_CorrectPassword_Accepted()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword("правильный-пароль");

        Assert.True(hasher.VerifyPassword("правильный-пароль", hash));
    }

    /// <summary>Неправильный пароль отклоняется.</summary>
    [Fact]
    public void VerifyPassword_WrongPassword_Rejected()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword("правильный-пароль");

        Assert.False(hasher.VerifyPassword("неправильный-пароль", hash));
    }

    /// <summary>Проверка чувствительна к регистру.</summary>
    [Fact]
    public void VerifyPassword_DifferentCase_Rejected()
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword("Пароль");

        Assert.False(hasher.VerifyPassword("пароль", hash));
    }

    /// <summary>
    /// Один и тот же пароль даёт разные хэши: BCrypt подмешивает случайную
    /// соль. Без этого одинаковые пароли разных пользователей были бы видны
    /// в базе как совпадающие строки.
    /// </summary>
    [Fact]
    public void HashPassword_SamePassword_ProducesDifferentHashes()
    {
        var hasher = CreateHasher();

        var first = hasher.HashPassword("один-и-тот-же");
        var second = hasher.HashPassword("один-и-тот-же");

        Assert.NotEqual(first, second);
        Assert.True(hasher.VerifyPassword("один-и-тот-же", first));
        Assert.True(hasher.VerifyPassword("один-и-тот-же", second));
    }

    /// <summary>Открытый пароль в хэш не попадает.</summary>
    [Fact]
    public void HashPassword_DoesNotContainOriginalPassword()
    {
        var hasher = CreateHasher();

        var hash = hasher.HashPassword("СекретноеСлово123");

        Assert.DoesNotContain("СекретноеСлово123", hash, StringComparison.Ordinal);
    }

    /// <summary>Кириллица и длинные пароли обрабатываются наравне с обычными.</summary>
    [Theory]
    [InlineData("пароль-по-русски")]
    [InlineData("Sp3c!@l#Ch$rs%^&*()")]
    [InlineData("оченьдлинныйпарольизмногихсимволовподряд1234567890")]
    public void HashPassword_VariousPasswords_VerifiedCorrectly(string password)
    {
        var hasher = CreateHasher();
        var hash = hasher.HashPassword(password);

        Assert.True(hasher.VerifyPassword(password, hash));
    }

    /// <summary>
    /// Испорченный хэш в базе не роняет вход, а трактуется как неверный пароль.
    /// Пустая строка даёт ArgumentException, а испорченная соль —
    /// SaltParseException: перехватывать нужно оба, иначе одна битая запись
    /// делает недоступным весь эндпоинт входа.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("не-хэш-вовсе")]
    [InlineData("$2a$обрезанный")]
    public void VerifyPassword_CorruptedHash_ReturnsFalseWithoutThrowing(string brokenHash)
    {
        var hasher = CreateHasher();

        var result = hasher.VerifyPassword("любой-пароль", brokenHash);

        Assert.False(result);
    }

    /// <summary>
    /// Хэш, посчитанный с одной стоимостью, проверяется и при другой:
    /// стоимость записана в самом хэше, поэтому её изменение в настройках
    /// не обесценивает уже заведённые учётные записи.
    /// </summary>
    [Fact]
    public void VerifyPassword_HashWithDifferentWorkFactor_VerifiedCorrectly()
    {
        var oldHasher = new PasswordHasher(workFactor: 4);
        var newHasher = new PasswordHasher(workFactor: 6);

        var hash = oldHasher.HashPassword("пароль");

        Assert.True(newHasher.VerifyPassword("пароль", hash));
    }
}
