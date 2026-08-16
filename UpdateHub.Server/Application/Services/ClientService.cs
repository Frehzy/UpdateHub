using AutoMapper;
using UpdateHub.Server.Api.V1.DTOs.Request;
using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Application.Sync;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Application.Services;

/// <summary>Управление компьютерами и их характеристиками.</summary>
/// <param name="clientRepository">Доступ к компьютерам.</param>
/// <param name="computerInfoRepository">Доступ к сведениям о железе.</param>
/// <param name="networkInfoRepository">Доступ к сетевым адресам.</param>
/// <param name="historyRepository">Доступ к истории изменений.</param>
/// <param name="blockHistoryRepository">Доступ к истории блокировок.</param>
/// <param name="groupRepository">Доступ к группам.</param>
/// <param name="mapper">Преобразование сущностей в модели ответа.</param>
/// <param name="logger">Журнал.</param>
public class ClientService(
    IClientRepository clientRepository,
    IClientComputerInfoRepository computerInfoRepository,
    IClientNetworkInfoRepository networkInfoRepository,
    IClientHistoryRepository historyRepository,
    IClientBlockHistoryRepository blockHistoryRepository,
    IGroupRepository groupRepository,
    IMapper mapper,
    ILogger<ClientService> logger) : IClientService
{
    /// <inheritdoc />
    public Task<ClientEntity?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default)
        => clientRepository.GetByIdAsync(clientId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<ClientEntity>> GetAllAsync(
        string? groupId,
        bool? isBlocked,
        string? search,
        CancellationToken cancellationToken = default)
        => clientRepository.SearchAsync(groupId, isBlocked, search, cancellationToken);

    /// <inheritdoc />
    public async Task<ClientDetailResponseDto> GetDetailAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetByIdWithDetailsAsync(clientId, cancellationToken)
            ?? throw new EntityNotFoundException($"Компьютер '{clientId}' не найден");

        var response = mapper.Map<ClientDetailResponseDto>(client);
        var history = await historyRepository.GetByClientIdAsync(clientId, 50, cancellationToken);
        response.History = mapper.Map<List<ClientHistoryResponseDto>>(history);

        return response;
    }

    /// <inheritdoc />
    public async Task<ClientEntity> CreateAsync(CreateClientRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
        {
            throw new ArgumentException("Идентификатор компьютера не может быть пустым");
        }

        if (await clientRepository.GetByIdAsync(request.ClientId, cancellationToken) is not null)
        {
            throw new InvalidOperationException($"Компьютер '{request.ClientId}' уже зарегистрирован");
        }

        if (!string.IsNullOrEmpty(request.GroupId) &&
            await groupRepository.GetByIdAsync(request.GroupId, cancellationToken) is null)
        {
            throw new EntityNotFoundException($"Группа '{request.GroupId}' не найдена");
        }

        var client = new ClientEntity
        {
            Id = request.ClientId,
            GroupId = request.GroupId,
            IsActive = true
        };

        await clientRepository.CreateAsync(client, cancellationToken);

        await computerInfoRepository.CreateAsync(new ClientComputerInfoEntity
        {
            ClientId = client.Id,
            Hostname = request.Name ?? "не указано"
        }, cancellationToken);

        await AddHistoryAsync(client.Id, ClientChangeType.Registered, null, request.Name, cancellationToken);

        logger.LogInformation("Зарегистрирован компьютер {ClientId} ({Name})", client.Id, request.Name);
        return client;
    }

    /// <inheritdoc />
    public async Task<ClientEntity> UpdateAsync(
        string clientId,
        UpdateClientRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetByIdAsync(clientId, cancellationToken)
            ?? throw new EntityNotFoundException($"Компьютер '{clientId}' не найден");

        if (request.GroupId is not null && request.GroupId != client.GroupId)
        {
            if (request.GroupId.Length > 0 &&
                await groupRepository.GetByIdAsync(request.GroupId, cancellationToken) is null)
            {
                throw new EntityNotFoundException($"Группа '{request.GroupId}' не найдена");
            }

            var oldGroupId = client.GroupId;
            client.GroupId = request.GroupId.Length == 0 ? null : request.GroupId;
            client.UpdatedAt = DateTime.UtcNow;

            await AddHistoryAsync(clientId, ClientChangeType.GroupChanged, oldGroupId, client.GroupId, cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var info = await computerInfoRepository.GetByClientIdAsync(clientId, cancellationToken);
            if (info is not null && info.Hostname != request.Name)
            {
                await AddHistoryAsync(clientId, ClientChangeType.HostnameChanged, info.Hostname, request.Name, cancellationToken);
                info.Hostname = request.Name;
                info.UpdatedAt = DateTime.UtcNow;
                await computerInfoRepository.UpdateAsync(info, cancellationToken);
            }
        }

        await clientRepository.UpdateAsync(client, cancellationToken);
        return client;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetByIdAsync(clientId, cancellationToken)
            ?? throw new EntityNotFoundException($"Компьютер '{clientId}' не найден");

        client.IsActive = false;
        client.UpdatedAt = DateTime.UtcNow;
        await clientRepository.UpdateAsync(client, cancellationToken);

        logger.LogInformation("Компьютер {ClientId} помечен удалённым", clientId);
    }

    /// <inheritdoc />
    public async Task BlockAsync(
        string clientId,
        string reason,
        string blockedBy,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetByIdAsync(clientId, cancellationToken)
            ?? throw new EntityNotFoundException($"Компьютер '{clientId}' не найден");

        client.IsBlocked = true;
        client.UpdatedAt = DateTime.UtcNow;
        await clientRepository.UpdateAsync(client, cancellationToken);

        await blockHistoryRepository.CreateAsync(new ClientBlockHistoryEntity
        {
            ClientId = clientId,
            Action = "blocked",
            Reason = reason,
            BlockedBy = blockedBy
        }, cancellationToken);

        await AddHistoryAsync(clientId, ClientChangeType.Blocked, null, reason, cancellationToken);
        logger.LogWarning("Компьютер {ClientId} заблокирован ({BlockedBy}): {Reason}", clientId, blockedBy, reason);
    }

    /// <inheritdoc />
    public async Task UnblockAsync(string clientId, string unblockedBy, CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetByIdAsync(clientId, cancellationToken)
            ?? throw new EntityNotFoundException($"Компьютер '{clientId}' не найден");

        client.IsBlocked = false;
        client.UpdatedAt = DateTime.UtcNow;
        await clientRepository.UpdateAsync(client, cancellationToken);

        await blockHistoryRepository.CreateAsync(new ClientBlockHistoryEntity
        {
            ClientId = clientId,
            Action = "unblocked",
            BlockedBy = unblockedBy
        }, cancellationToken);

        await AddHistoryAsync(clientId, ClientChangeType.Unblocked, null, null, cancellationToken);
        logger.LogInformation("Компьютер {ClientId} разблокирован ({UnblockedBy})", clientId, unblockedBy);
    }

    /// <inheritdoc />
    public async Task RecordCheckInAsync(
        string clientId,
        ClientReport report,
        ConnectionContext context,
        CancellationToken cancellationToken = default)
    {
        await UpdateComputerInfoAsync(clientId, report, cancellationToken);

        if (!string.IsNullOrEmpty(context.RemoteIpAddress))
        {
            await UpdateNetworkInfoAsync(clientId, context.RemoteIpAddress, report.MacAddress, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task AddHistoryAsync(
        string clientId,
        ClientChangeType changeType,
        string? oldValue,
        string? newValue,
        CancellationToken cancellationToken = default)
    {
        await historyRepository.CreateAsync(new ClientHistoryEntity
        {
            ClientId = clientId,
            ChangeType = changeType,
            OldValue = oldValue,
            NewValue = newValue
        }, cancellationToken);
    }

    /// <summary>
    /// Сверяет присланные сведения о железе с сохранёнными и фиксирует расхождения.
    /// </summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="report">Присланные сведения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    private async Task UpdateComputerInfoAsync(string clientId, ClientReport report, CancellationToken cancellationToken)
    {
        var info = await computerInfoRepository.GetByClientIdAsync(clientId, cancellationToken);
        if (info is null)
        {
            info = new ClientComputerInfoEntity { ClientId = clientId, Hostname = report.Hostname ?? "не указано" };
            await computerInfoRepository.CreateAsync(info, cancellationToken);
        }

        var changed = false;

        changed |= await ApplyAsync(report.Hostname, info.Hostname, ClientChangeType.HostnameChanged,
            v => info.Hostname = v!, clientId, cancellationToken);
        changed |= await ApplyAsync(report.HardwareFingerprint, info.HardwareFingerprint, ClientChangeType.HardwareFingerprintChanged,
            v => info.HardwareFingerprint = v, clientId, cancellationToken);
        changed |= await ApplyAsync(report.OsVersion, info.OsVersion, ClientChangeType.OsVersionChanged,
            v => info.OsVersion = v, clientId, cancellationToken);
        changed |= await ApplyAsync(report.KernelVersion, info.KernelVersion, ClientChangeType.KernelVersionChanged,
            v => info.KernelVersion = v, clientId, cancellationToken);
        changed |= await ApplyAsync(report.Architecture, info.Architecture, ClientChangeType.ArchitectureChanged,
            v => info.Architecture = v, clientId, cancellationToken);
        changed |= await ApplyAsync(report.CpuInfo, info.CpuInfo, ClientChangeType.CpuInfoChanged,
            v => info.CpuInfo = v, clientId, cancellationToken);
        changed |= await ApplyAsync(report.MemoryGb?.ToString(), info.MemoryGb?.ToString(), ClientChangeType.MemoryChanged,
            _ => info.MemoryGb = report.MemoryGb, clientId, cancellationToken);
        changed |= await ApplyAsync(report.DiskGb?.ToString(), info.DiskGb?.ToString(), ClientChangeType.DiskChanged,
            _ => info.DiskGb = report.DiskGb, clientId, cancellationToken);

        if (changed)
        {
            info.UpdatedAt = DateTime.UtcNow;
            await computerInfoRepository.UpdateAsync(info, cancellationToken);
        }
    }

    /// <summary>
    /// Применяет новое значение характеристики, если оно прислано и отличается от текущего.
    /// </summary>
    /// <param name="incoming">Присланное значение.</param>
    /// <param name="current">Текущее значение.</param>
    /// <param name="changeType">Тип изменения для истории.</param>
    /// <param name="apply">Действие, записывающее значение в сущность.</param>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если значение изменилось.</returns>
    private async Task<bool> ApplyAsync(
        string? incoming,
        string? current,
        ClientChangeType changeType,
        Action<string?> apply,
        string clientId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(incoming) || incoming == current)
        {
            return false;
        }

        await AddHistoryAsync(clientId, changeType, current, incoming, cancellationToken);
        apply(incoming);
        return true;
    }

    /// <summary>
    /// Отмечает обращение с адреса и снимает признак активности с прежних адресов.
    /// </summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="ipAddress">Адрес соединения.</param>
    /// <param name="macAddress">MAC-адрес, если клиент его сообщил.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    private async Task UpdateNetworkInfoAsync(
        string clientId,
        string ipAddress,
        string? macAddress,
        CancellationToken cancellationToken)
    {
        var existing = await networkInfoRepository.GetByClientAndIpAsync(clientId, ipAddress, cancellationToken);

        if (existing is not null)
        {
            if (macAddress is not null && existing.MacAddress != macAddress)
            {
                await AddHistoryAsync(clientId, ClientChangeType.MacAddressChanged, existing.MacAddress, macAddress, cancellationToken);
                existing.MacAddress = macAddress;
            }

            existing.LastSeen = DateTime.UtcNow;
            existing.IsActive = true;
            await networkInfoRepository.UpdateAsync(existing, cancellationToken);
        }
        else
        {
            await networkInfoRepository.CreateAsync(new ClientNetworkInfoEntity
            {
                ClientId = clientId,
                IpAddress = ipAddress,
                MacAddress = macAddress,
                LastSeen = DateTime.UtcNow,
                IsActive = true
            }, cancellationToken);

            await AddHistoryAsync(clientId, ClientChangeType.IpChanged, null, ipAddress, cancellationToken);
        }

        var others = await networkInfoRepository.GetByClientIdAsync(clientId, cancellationToken);
        foreach (var other in others.Where(r => r.IsActive && r.IpAddress != ipAddress))
        {
            other.IsActive = false;
            await networkInfoRepository.UpdateAsync(other, cancellationToken);
        }
    }
}
