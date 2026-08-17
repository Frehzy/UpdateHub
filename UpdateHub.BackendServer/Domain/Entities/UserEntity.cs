using UpdateHub.Shared.Enums;

namespace UpdateHub.BackendServer.Domain.Entities;

/// <summary>
/// Учётная запись пользователя сервера обновлений.
/// </summary>
/// <remarks>
/// Один пользователь может работать за несколькими компьютерами, но только
/// за теми, на которые администратор выдал права — напрямую
/// (<see cref="UserClientAccessEntity"/>) или через группу
/// (<see cref="UserGroupAccessEntity"/>).
/// </remarks>
public class UserEntity
{
    /// <summary>Первичный ключ.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Логин. Уникален в пределах системы.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Хэш пароля BCrypt. Открытый пароль нигде не хранится.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Роль: обычный пользователь или администратор.</summary>
    public UserRole Role { get; set; }

    /// <summary>Момент создания учётной записи.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Момент последнего успешного входа.</summary>
    public DateTime? LastLogin { get; set; }

    /// <summary>
    /// Признак активности. Отключённая учётная запись не может войти,
    /// а все её refresh-токены отзываются.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Требование сменить пароль при следующем входе. Выставляется
    /// администратору, создаваемому при первом запуске, и новым пользователям.
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Выданные refresh-токены.</summary>
    public ICollection<RefreshTokenEntity> RefreshTokens { get; set; } = [];

    /// <summary>Персональные разрешения на конкретные компьютеры.</summary>
    public ICollection<UserClientAccessEntity> UserClientAccesses { get; set; } = [];

    /// <summary>Разрешения на группы компьютеров.</summary>
    public ICollection<UserGroupAccessEntity> UserGroupAccesses { get; set; } = [];
}
