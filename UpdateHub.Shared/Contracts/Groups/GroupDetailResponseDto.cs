using UpdateHub.Shared.Contracts.Clients;

namespace UpdateHub.Shared.Contracts.Groups;

/// <summary>Группа компьютеров вместе с её составом.</summary>
public class GroupDetailResponseDto : GroupResponseDto
{
    /// <summary>Компьютеры, входящие в группу.</summary>
    public List<ClientResponseDto> Clients { get; set; } = [];
}
