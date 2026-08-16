namespace UpdateHub.Server.Domain.Entities;

/// <summary>
/// Персональное разрешение пользователя работать за конкретным компьютером.
/// </summary>
public class UserClientAccessEntity
{
    /// <summary>Первичный ключ.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Пользователь, которому выдано разрешение.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Компьютер, на который выдано разрешение.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Момент выдачи разрешения.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Пользователь (навигационное свойство).</summary>
    public UserEntity? User { get; set; }

    /// <summary>Компьютер (навигационное свойство).</summary>
    public ClientEntity? Client { get; set; }
}
