using System.ComponentModel.DataAnnotations;

namespace UpdateHub.Shared.Contracts.Groups;

/// <summary>Создание группы компьютеров.</summary>
public class CreateGroupRequestDto
{
    /// <summary>Название группы.</summary>
    [Required(ErrorMessage = "Не указано название группы")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Описание группы.</summary>
    public string? Description { get; set; }
}
