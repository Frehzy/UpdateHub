namespace UpdateHub.Server.Api.V1.DTOs.Request;

public class CreateClientRequestDto
{
    public string ClientId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? GroupId { get; set; }
    public string? Description { get; set; }
}