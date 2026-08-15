namespace UpdateHub.Server.Api.V1.DTOs.Request;

public class CreateGroupRequestDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}