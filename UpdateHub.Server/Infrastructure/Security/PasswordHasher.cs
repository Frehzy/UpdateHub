using BCryptNet = BCrypt.Net.BCrypt;

namespace UpdateHub.Server.Infrastructure.Security;

public class PasswordHasher(int saltRounds = 12)
{
    public string HashPassword(string password)
    {
        return BCryptNet.HashPassword(password, saltRounds);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return BCryptNet.Verify(password, hash);
    }
}