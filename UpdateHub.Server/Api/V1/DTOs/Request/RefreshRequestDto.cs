using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace UpdateHub.Server.Api.V1.DTOs.Request;

/// <summary>Обмен refresh-токена на новую пару токенов.</summary>
public class RefreshRequestDto
{
    /// <summary>Действующий refresh-токен.</summary>
    [Required(ErrorMessage = "Не указан refresh-токен")]
    [FromForm(Name = "refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;
}
