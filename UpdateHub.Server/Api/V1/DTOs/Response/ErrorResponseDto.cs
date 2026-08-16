namespace UpdateHub.Server.Api.V1.DTOs.Response;

/// <summary>Описание ошибки, возвращаемое панели управления.</summary>
public class ErrorResponseDto
{
    /// <summary>Сообщение об ошибке на русском языке.</summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>Уточняющие сведения, например список непрошедших проверку полей.</summary>
    public IReadOnlyList<string>? Details { get; set; }
}
