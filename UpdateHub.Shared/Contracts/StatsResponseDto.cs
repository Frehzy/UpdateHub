namespace UpdateHub.Shared.Contracts;

/// <summary>Сводная статистика обращений.</summary>
public class StatsResponseDto
{
    /// <summary>Общее число обращений за период.</summary>
    public int TotalRequests { get; set; }

    /// <summary>Число различных компьютеров, обращавшихся за период.</summary>
    public int UniqueClients { get; set; }

    /// <summary>Суммарный объём файлов, предложенных к скачиванию.</summary>
    public long TotalDownloadedBytes { get; set; }

    /// <summary>Число обращений по дням.</summary>
    public List<StatsDayDto> RequestsByDay { get; set; } = [];
}
