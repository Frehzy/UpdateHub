namespace UpdateHub.Server.Api.V1.DTOs.Response;

/// <summary>Группа компьютеров вместе с её составом.</summary>
public class GroupDetailResponseDto : GroupResponseDto
{
    /// <summary>Компьютеры, входящие в группу.</summary>
    public List<ClientResponseDto> Clients { get; set; } = [];
}
