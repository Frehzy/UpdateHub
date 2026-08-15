using AutoMapper;
using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Services;

public class GroupService(
    IGroupRepository groupRepository,
    IUserRepository userRepository,
    IUserClientAccessRepository userClientAccessRepository,
    IUserGroupAccessRepository userGroupAccessRepository,
    IClientRepository clientRepository,
    IMapper mapper,
    ILogger<GroupService> logger) : IGroupService
{
    public async Task<GroupEntity> CreateGroupAsync(string name, string? description)
    {
        if (await groupRepository.GetByNameAsync(name) != null)
        {
            throw new InvalidOperationException($"Group '{name}' already exists");
        }

        var group = new GroupEntity
        {
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        return await groupRepository.CreateAsync(group);
    }

    public async Task<GroupEntity> UpdateGroupAsync(string groupId, string? name, string? description)
    {
        var group = await groupRepository.GetByIdAsync(groupId) ?? throw new ArgumentException($"Group {groupId} not found");
        if (!string.IsNullOrEmpty(name) && name != group.Name)
        {
            var existing = await groupRepository.GetByNameAsync(name);
            if (existing != null && existing.Id != groupId)
            {
                throw new InvalidOperationException($"Group '{name}' already exists");
            }
            group.Name = name;
        }

        if (description != null)
            group.Description = description;

        group.UpdatedAt = DateTime.UtcNow;
        return await groupRepository.UpdateAsync(group);
    }

    public async Task DeleteGroupAsync(string groupId)
    {
        var group = await groupRepository.GetByIdAsync(groupId) ?? throw new ArgumentException($"Group {groupId} not found");
        group.IsActive = false;
        group.UpdatedAt = DateTime.UtcNow;
        await groupRepository.UpdateAsync(group);
    }

    public async Task<GroupEntity?> GetGroupByIdAsync(string groupId)
    {
        return await groupRepository.GetByIdAsync(groupId);
    }

    public async Task<IEnumerable<GroupResponseDto>> GetAllGroupsAsync()
    {
        var groups = await groupRepository.GetActiveGroupsAsync();
        return mapper.Map<IEnumerable<GroupResponseDto>>(groups);
    }

    public async Task<GroupDetailResponseDto> GetGroupDetailAsync(string groupId)
    {
        var group = await groupRepository.GetByIdAsync(groupId) ?? throw new ArgumentException($"Group {groupId} not found");
        var response = mapper.Map<GroupDetailResponseDto>(group);
        var clients = await clientRepository.GetAllAsync(groupId);
        response.Clients = mapper.Map<List<ClientResponseDto>>(clients);
        return response;
    }

    public async Task AddUserClientAccessAsync(string userId, string clientId)
    {
        _ = await userRepository.GetByIdAsync(userId) ?? throw new ArgumentException("User not found");
        _ = await clientRepository.GetByIdAsync(clientId) ?? throw new ArgumentException("Client not found");
        var existing = await userClientAccessRepository.GetByUserAndClientAsync(userId, clientId);
        if (existing != null)
        {
            throw new InvalidOperationException("User already has access to this client");
        }

        var access = new UserClientAccessEntity
        {
            UserId = userId,
            ClientId = clientId,
            CreatedAt = DateTime.UtcNow
        };

        await userClientAccessRepository.CreateAsync(access);
    }

    public async Task RemoveUserClientAccessAsync(string userId, string clientId)
    {
        var access = await userClientAccessRepository.GetByUserAndClientAsync(userId, clientId) ?? throw new ArgumentException("Access not found");
        await userClientAccessRepository.DeleteAsync(access.Id);
    }

    public async Task AddUserGroupAccessAsync(string userId, string groupId)
    {
        _ = await userRepository.GetByIdAsync(userId) ?? throw new ArgumentException("User not found");
        _ = await groupRepository.GetByIdAsync(groupId) ?? throw new ArgumentException("Group not found");
        var existing = await userGroupAccessRepository.GetByUserAndGroupAsync(userId, groupId);
        if (existing != null)
        {
            throw new InvalidOperationException("User already has access to this group");
        }

        var access = new UserGroupAccessEntity
        {
            UserId = userId,
            GroupId = groupId,
            CreatedAt = DateTime.UtcNow
        };

        await userGroupAccessRepository.CreateAsync(access);
    }

    public async Task RemoveUserGroupAccessAsync(string userId, string groupId)
    {
        var access = await userGroupAccessRepository.GetByUserAndGroupAsync(userId, groupId) ?? throw new ArgumentException("Access not found");
        await userGroupAccessRepository.DeleteAsync(access.Id);
    }
}