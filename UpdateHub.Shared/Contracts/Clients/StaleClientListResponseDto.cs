namespace UpdateHub.Shared.Contracts.Clients;

/// <summary>Список компьютеров, давно не выходивших на связь.</summary>
public class StaleClientListResponseDto
{
    /// <summary>Компьютеры.</summary>
    public List<StaleClientDto> Clients { get; set; } = [];

    /// <summary>Общее количество.</summary>
    public int Total { get; set; }

    /// <summary>Порог в сутках, по которому отбирались компьютеры.</summary>
    public int ThresholdDays { get; set; }
}
