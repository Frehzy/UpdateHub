using System.ComponentModel.DataAnnotations;

namespace UpdateHub.Server.Api.V1.DTOs.Request;

/// <summary>Создание группы компьютеров.</summary>
public class CreateGroupRequestDto
{
    /// <summary>Название группы.</summary>
    [Required(ErrorMessage = "Не указано название группы")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Описание группы.</summary>
    public string? Description { get; set; }
}
