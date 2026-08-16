namespace UpdateHub.Server.Domain.Entities;

/// <summary>
/// Группа компьютеров. Права пользователя на группу распространяются
/// на все входящие в неё компьютеры.
/// </summary>
public class GroupEntity
{
    /// <summary>Первичный ключ.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Название группы. Уникально среди активных групп.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Произвольное описание.</summary>
    public string? Description { get; set; }

    /// <summary>Момент создания.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Момент последнего изменения.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Признак активности; снимается при «мягком удалении».</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Компьютеры, входящие в группу.</summary>
    public ICollection<ClientEntity> Clients { get; set; } = [];

    /// <summary>Разрешения пользователей на эту группу.</summary>
    public ICollection<UserGroupAccessEntity> UserGroupAccesses { get; set; } = [];
}
