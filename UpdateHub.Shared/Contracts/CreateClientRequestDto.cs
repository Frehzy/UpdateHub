using System.ComponentModel.DataAnnotations;

namespace UpdateHub.Shared.Contracts;

/// <summary>Регистрация компьютера администратором.</summary>
public class CreateClientRequestDto
{
    /// <summary>Идентификатор компьютера из <c>/etc/updatehub/client-id</c>.</summary>
    [Required(ErrorMessage = "Не указан идентификатор компьютера")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Отображаемое имя компьютера.</summary>
    public string? Name { get; set; }

    /// <summary>Группа, в которую поместить компьютер.</summary>
    public string? GroupId { get; set; }
}
