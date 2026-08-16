using BCryptNet = BCrypt.Net.BCrypt;

namespace UpdateHub.Server.Infrastructure.Security;

/// <summary>
/// Хэширование и проверка паролей алгоритмом BCrypt.
/// </summary>
/// <param name="workFactor">
/// Стоимость вычисления: каждая единица удваивает время. Значение 12 даёт
/// примерно 0,3 секунды на проверку — достаточно, чтобы перебор был бессмысленным,
/// и незаметно при единичном входе.
/// </param>
public class PasswordHasher(int workFactor = 12)
{
    /// <summary>Вычисляет хэш пароля.</summary>
    /// <param name="password">Открытый пароль.</param>
    /// <returns>Строка хэша, включающая соль и стоимость.</returns>
    public string HashPassword(string password) => BCryptNet.HashPassword(password, workFactor);

    /// <summary>Проверяет пароль по хэшу.</summary>
    /// <param name="password">Проверяемый пароль.</param>
    /// <param name="hash">Ранее вычисленный хэш.</param>
    /// <returns><see langword="true"/>, если пароль верен.</returns>
    public bool VerifyPassword(string password, string hash)
    {
        // Повреждённый или пустой хэш в базе не должен ронять вход целиком.
        // Пустая строка даёт ArgumentException, а испорченная соль —
        // SaltParseException: ловить нужно оба, иначе одна битая запись
        // в таблице пользователей делает недоступным весь вход в систему.
        try
        {
            return BCryptNet.Verify(password, hash);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException or FormatException)
        {
            return false;
        }
    }
}
