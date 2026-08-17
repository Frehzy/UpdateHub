using AutoMapper;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Application.Sync;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Shared.Contracts;

namespace UpdateHub.Server.Application.Services;

/// <summary>Управление группами компьютеров и выдачей прав.</summary>
/// <param name="groupRepository">Доступ к группам.</param>
/// <param name="userRepository">Доступ к учётным записям.</param>
/// <param name="clientRepository">Доступ к компьютерам.</param>
/// <param name="userClientAccessRepository">Доступ к персональным разрешениям.</param>
/// <param name="userGroupAccessRepository">Доступ к разрешениям на группы.</param>
/// <param name="mapper">Преобразование сущностей в модели ответа.</param>
/// <param name="logger">Журнал.</param>
public class GroupService(
    IGroupRepository groupRepository,
    IUserRepository userRepository,
    IClientRepository clientRepository,
    IUserClientAccessRepository userClientAccessRepository,
    IUserGroupAccessRepository userGroupAccessRepository,
    IMapper mapper,
    ILogger<GroupService> logger) : IGroupService
{
    /// <inheritdoc />
    public async Task<GroupEntity> CreateAsync(string name, string? description, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Название группы не может быть пустым");
        }

        if (await groupRepository.GetByNameAsync(name, cancellationToken) is not null)
        {
            throw new InvalidOperationException($"Группа '{name}' уже существует");
        }

        var group = new GroupEntity { Name = name, Description = description };
        await groupRepository.CreateAsync(group, cancellationToken);

        logger.LogInformation("Создана группа {Name}", name);
        return group;
    }

    /// <inheritdoc />
    public async Task<GroupEntity> UpdateAsync(
        string groupId,
        string? name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        var group = await groupRepository.GetByIdAsync(groupId, cancellationToken)
            ?? throw new EntityNotFoundException($"Группа '{groupId}' не найдена");

        if (!string.IsNullOrWhiteSpace(name) && name != group.Name)
        {
            var rival = await groupRepository.GetByNameAsync(name, cancellationToken);
            if (rival is not null && rival.Id != groupId)
            {
                throw new InvalidOperationException($"Группа '{name}' уже существует");
            }

            group.Name = name;
        }

        if (description is not null)
        {
            group.Description = description;
        }

        group.UpdatedAt = DateTime.UtcNow;
        await groupRepository.UpdateAsync(group, cancellationToken);

        return group;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var group = await groupRepository.GetByIdAsync(groupId, cancellationToken)
            ?? throw new EntityNotFoundException($"Группа '{groupId}' не найдена");

        group.IsActive = false;
        group.UpdatedAt = DateTime.UtcNow;
        await groupRepository.UpdateAsync(group, cancellationToken);

        logger.LogInformation("Группа {Name} помечена удалённой", group.Name);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GroupResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var groups = await groupRepository.GetActiveAsync(cancellationToken);
        return mapper.Map<List<GroupResponseDto>>(groups);
    }

    /// <inheritdoc />
    public async Task<GroupDetailResponseDto> GetDetailAsync(string groupId, CancellationToken cancellationToken = default)
    {
        var group = await groupRepository.GetByIdAsync(groupId, cancellationToken)
            ?? throw new EntityNotFoundException($"Группа '{groupId}' не найдена");

        var response = mapper.Map<GroupDetailResponseDto>(group);
        var clients = await clientRepository.SearchAsync(groupId, cancellationToken: cancellationToken);
        response.Clients = mapper.Map<List<ClientResponseDto>>(clients);

        return response;
    }

    /// <inheritdoc />
    public async Task GrantClientAccessAsync(string userId, string clientId, CancellationToken cancellationToken = default)
    {
        _ = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException($"Пользователь '{userId}' не найден");
        _ = await clientRepository.GetByIdAsync(clientId, cancellationToken)
            ?? throw new EntityNotFoundException($"Компьютер '{clientId}' не найден");

        if (await userClientAccessRepository.ExistsAsync(userId, clientId, cancellationToken))
        {
            return;
        }

        await userClientAccessRepository.CreateAsync(
            new UserClientAccessEntity { UserId = userId, ClientId = clientId },
            cancellationToken);

        logger.LogInformation("Пользователю {UserId} выданы права на компьютер {ClientId}", userId, clientId);
    }

    /// <inheritdoc />
    public async Task RevokeClientAccessAsync(string userId, string clientId, CancellationToken cancellationToken = default)
    {
        var access = await userClientAccessRepository.GetAsync(userId, clientId, cancellationToken)
            ?? throw new EntityNotFoundException("Разрешение не найдено");

        await userClientAccessRepository.DeleteAsync(access.Id, cancellationToken);
        logger.LogInformation("У пользователя {UserId} отозваны права на компьютер {ClientId}", userId, clientId);
    }

    /// <inheritdoc />
    public async Task GrantGroupAccessAsync(string userId, string groupId, CancellationToken cancellationToken = default)
    {
        _ = await userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new EntityNotFoundException($"Пользователь '{userId}' не найден");
        _ = await groupRepository.GetByIdAsync(groupId, cancellationToken)
            ?? throw new EntityNotFoundException($"Группа '{groupId}' не найдена");

        if (await userGroupAccessRepository.ExistsAsync(userId, groupId, cancellationToken))
        {
            return;
        }

        await userGroupAccessRepository.CreateAsync(
            new UserGroupAccessEntity { UserId = userId, GroupId = groupId },
            cancellationToken);

        logger.LogInformation("Пользователю {UserId} выданы права на группу {GroupId}", userId, groupId);
    }

    /// <inheritdoc />
    public async Task RevokeGroupAccessAsync(string userId, string groupId, CancellationToken cancellationToken = default)
    {
        var access = await userGroupAccessRepository.GetAsync(userId, groupId, cancellationToken)
            ?? throw new EntityNotFoundException("Разрешение не найдено");

        await userGroupAccessRepository.DeleteAsync(access.Id, cancellationToken);
        logger.LogInformation("У пользователя {UserId} отозваны права на группу {GroupId}", userId, groupId);
    }
}
