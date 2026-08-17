using System.ComponentModel.DataAnnotations;
using UpdateHub.Shared.Enums;

namespace UpdateHub.Shared.Contracts;

/// <summary>Создание учётной записи администратором.</summary>
public class CreateUserRequestDto
{
    /// <summary>Логин.</summary>
    [Required(ErrorMessage = "Не указан логин")]
    public string Username { get; set; } = string.Empty;

    /// <summary>Начальный пароль; при первом входе потребуется его сменить.</summary>
    [Required(ErrorMessage = "Не указан пароль")]
    [MinLength(8, ErrorMessage = "Пароль должен содержать не менее 8 символов")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Роль пользователя.</summary>
    public UserRole Role { get; set; } = UserRole.Client;

    /// <summary>Группы компьютеров, к которым сразу выдаётся доступ.</summary>
    public List<string>? GroupIds { get; set; }

    /// <summary>Компьютеры, к которым сразу выдаётся доступ.</summary>
    public List<string>? ClientIds { get; set; }
}
