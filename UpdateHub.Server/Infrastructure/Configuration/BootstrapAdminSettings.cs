namespace UpdateHub.Server.Infrastructure.Configuration;

/// <summary>
/// Учётные данные администратора, создаваемого при первом запуске на пустой базе.
/// Читаются из секции <c>BootstrapAdmin</c>.
/// </summary>
/// <remarks>
/// Нужны потому, что завести пользователя можно только под администратором,
/// а на пустой базе администратора ещё нет. После первого входа пароль
/// обязателен к смене — учётной записи выставляется
/// <see cref="Domain.Entities.UserEntity.MustChangePassword"/>.
/// Если пароль не задан, сервер сгенерирует случайный и один раз напечатает его в лог.
/// </remarks>
public class BootstrapAdminSettings
{
    /// <summary>Логин создаваемого администратора.</summary>
    public string Username { get; set; } = "admin";

    /// <summary>
    /// Пароль администратора. Если пусто — будет сгенерирован случайный
    /// и выведен в лог при старте (единственный раз, повторно узнать его нельзя).
    /// </summary>
    public string? Password { get; set; }
}
