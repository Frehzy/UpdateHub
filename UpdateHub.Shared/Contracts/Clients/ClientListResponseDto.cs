namespace UpdateHub.Shared.Contracts.Clients;

/// <summary>Список компьютеров.</summary>
public class ClientListResponseDto
{
    /// <summary>Компьютеры.</summary>
    public List<ClientResponseDto> Clients { get; set; } = [];

    /// <summary>Общее количество.</summary>
    public int Total { get; set; }
}
