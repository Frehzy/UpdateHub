using UpdateHub.BackendServer.Domain.Entities.Groups;

namespace UpdateHub.BackendServer.Domain.Entities.Users;

/// <summary>
/// Разрешение пользователя работать за любым компьютером группы.
/// </summary>
public class UserGroupAccessEntity
{
    /// <summary>Первичный ключ.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Пользователь, которому выдано разрешение.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Группа компьютеров, на которую выдано разрешение.</summary>
    public string GroupId { get; set; } = string.Empty;

    /// <summary>Момент выдачи разрешения.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Пользователь (навигационное свойство).</summary>
    public UserEntity? User { get; set; }

    /// <summary>Группа (навигационное свойство).</summary>
    public GroupEntity? Group { get; set; }
}
