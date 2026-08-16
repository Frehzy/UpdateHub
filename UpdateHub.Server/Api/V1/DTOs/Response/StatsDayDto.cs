namespace UpdateHub.Server.Api.V1.DTOs.Response;

/// <summary>Число обращений за одни сутки.</summary>
public class StatsDayDto
{
    /// <summary>Дата.</summary>
    public DateTime Date { get; set; }

    /// <summary>Число обращений.</summary>
    public int Count { get; set; }
}
