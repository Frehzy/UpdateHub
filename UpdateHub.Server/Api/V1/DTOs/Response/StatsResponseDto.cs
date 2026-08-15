namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class StatsResponseDto
{
    public int TotalRequests { get; set; }
    public int UniqueClients { get; set; }
    public long TotalDownloadedBytes { get; set; }
    public List<StatsDayDto>? RequestsByDay { get; set; }
}