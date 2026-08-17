using System.ComponentModel.DataAnnotations;

namespace UpdateHub.Shared.Contracts;

/// <summary>Блокировка компьютера.</summary>
public class BlockClientRequestDto
{
    /// <summary>Причина блокировки; показывается пользователю при отказе в доступе.</summary>
    [Required(ErrorMessage = "Не указана причина блокировки")]
    public string Reason { get; set; } = string.Empty;
}
