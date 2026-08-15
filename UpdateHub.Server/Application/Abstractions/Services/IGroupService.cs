using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Services;

public interface IGroupService
{
    Task<GroupEntity> CreateGroupAsync(string name, string? description);
    Task<GroupEntity> UpdateGroupAsync(string groupId, string? name, string? description);
    Task DeleteGroupAsync(string groupId);
    Task<GroupEntity?> GetGroupByIdAsync(string groupId);
    Task<IEnumerable<GroupResponseDto>> GetAllGroupsAsync();
    Task<GroupDetailResponseDto> GetGroupDetailAsync(string groupId);
    Task AddUserClientAccessAsync(string userId, string clientId);
    Task RemoveUserClientAccessAsync(string userId, string clientId);
    Task AddUserGroupAccessAsync(string userId, string groupId);
    Task RemoveUserGroupAccessAsync(string userId, string groupId);
}