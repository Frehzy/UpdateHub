namespace UpdateHub.Shared.Contracts.Clients;

/// <summary>
/// Компьютер, давно не выходивший на связь.
/// </summary>
/// <remarks>
/// Отвечает на вопрос, который администратор задаёт каждое утро: какие машины
/// перестали обновляться. Причины бывают разные — машину выключили, скрипт
/// не установлен, права отозваны, — но узнать о самом факте нужно раньше,
/// чем через полгода.
/// </remarks>
public class StaleClientDto
{
    /// <summary>Идентификатор компьютера.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Имя машины, если оно известно.</summary>
    public string? Name { get; set; }

    /// <summary>Название группы, если компьютер в неё входит.</summary>
    public string? GroupName { get; set; }

    /// <summary>
    /// Когда компьютер обращался в последний раз.
    /// </summary>
    /// <remarks>
    /// Пусто, если он не обращался ни разу: заведён администратором, но скрипт
    /// на нём так и не заработал. Это худший случай из возможных, и он должен
    /// быть виден отдельно.
    /// </remarks>
    public DateTime? LastRequestAt { get; set; }

    /// <summary>Сколько суток прошло с последнего обращения.</summary>
    public int? DaysSinceLastRequest { get; set; }

    /// <summary>Заблокирован ли компьютер администратором.</summary>
    public bool IsBlocked { get; set; }
}
