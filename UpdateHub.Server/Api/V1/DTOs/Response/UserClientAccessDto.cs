namespace UpdateHub.Server.Api.V1.DTOs.Response;

/// <summary>Персональное разрешение пользователя на компьютер.</summary>
public class UserClientAccessDto
{
    /// <summary>Идентификатор компьютера.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Отображаемое имя компьютера.</summary>
    public string? ClientName { get; set; }

    /// <summary>Момент выдачи разрешения.</summary>
    public DateTime CreatedAt { get; set; }
}
