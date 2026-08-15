namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class ClientHistoryResponseDto
{
    public int Id { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangeTimestamp { get; set; }
}