namespace UpdateHub.Server.Api.V1.DTOs.Response;

public class GroupDetailResponseDto : GroupResponseDto
{
    public List<ClientResponseDto> Clients { get; set; } = [];
}