namespace UpdateHub.Shared.Contracts;

/// <summary>Учётная запись в панели управления.</summary>
public class UserResponseDto
{
    /// <summary>Идентификатор пользователя.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Логин.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Роль.</summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>Признак активности.</summary>
    public bool IsActive { get; set; }

    /// <summary>Требуется ли смена пароля при следующем входе.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Момент создания.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Момент последнего входа.</summary>
    public DateTime? LastLogin { get; set; }

    /// <summary>Компьютеры, на которые выданы персональные права.</summary>
    public List<UserClientAccessDto>? ClientAccesses { get; set; }

    /// <summary>Группы, на которые выданы права.</summary>
    public List<UserGroupAccessDto>? GroupAccesses { get; set; }
}
