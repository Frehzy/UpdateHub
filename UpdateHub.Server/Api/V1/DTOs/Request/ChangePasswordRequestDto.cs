using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace UpdateHub.Server.Api.V1.DTOs.Request;

/// <summary>Смена собственного пароля.</summary>
public class ChangePasswordRequestDto
{
    /// <summary>Текущий пароль.</summary>
    [Required(ErrorMessage = "Не указан текущий пароль")]
    [FromForm(Name = "current_password")]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>Новый пароль, не короче восьми символов.</summary>
    [Required(ErrorMessage = "Не указан новый пароль")]
    [MinLength(8, ErrorMessage = "Пароль должен содержать не менее 8 символов")]
    [FromForm(Name = "new_password")]
    public string NewPassword { get; set; } = string.Empty;
}
