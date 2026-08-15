using AutoMapper;
using UpdateHub.Server.Api.V1.DTOs.Request;
using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Application.Services;

public class ClientService(
    IClientRepository clientRepository,
    IClientComputerInfoRepository clientComputerInfoRepository,
    IClientNetworkInfoRepository clientNetworkInfoRepository,
    IClientHistoryRepository clientHistoryRepository,
    IClientBlockHistoryRepository clientBlockHistoryRepository,
    IGroupRepository groupRepository,
    IMapper mapper,
    ILogger<ClientService> logger) : IClientService
{
    public async Task<ClientEntity> GetOrCreateClientAsync(ClientInfoDto clientInfo)
    {
        if (clientInfo == null || string.IsNullOrEmpty(clientInfo.ClientId))
        {
            throw new ArgumentException("Client info is required");
        }

        var client = await clientRepository.GetByIdAsync(clientInfo.ClientId);
        if (client == null)
        {
            logger.LogInformation("Creating new client: {ClientId} ({Hostname})",
                clientInfo.ClientId, clientInfo.Hostname);

            client = new ClientEntity
            {
                Id = clientInfo.ClientId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            client = await clientRepository.CreateAsync(client);

            // Создаём информацию о компьютере
            await clientComputerInfoRepository.CreateAsync(new ClientComputerInfoEntity
            {
                ClientId = client.Id,
                Hostname = clientInfo.Hostname ?? "unknown",
                OsVersion = clientInfo.OsVersion,
                CpuInfo = clientInfo.CpuInfo,
                MemoryGb = clientInfo.MemoryGb,
                DiskGb = clientInfo.DiskGb,
                Architecture = clientInfo.Architecture,
                KernelVersion = clientInfo.KernelVersion,
                UpdatedAt = DateTime.UtcNow
            });

            // Добавляем запись в историю
            await AddClientHistoryAsync(client.Id, ClientChangeType.SessionCreated.ToString(), null, "Client registered", null);
        }
        else
        {
            // Обновляем информацию о компьютере
            await UpdateClientComputerInfoAsync(client.Id, clientInfo);
        }

        // Обновляем сетевую информацию
        if (!string.IsNullOrEmpty(clientInfo.IpAddress))
        {
            await UpdateClientNetworkInfoAsync(client.Id, clientInfo.IpAddress);
        }

        client.UpdatedAt = DateTime.UtcNow;
        await clientRepository.UpdateAsync(client);

        return client;
    }

    public async Task<ClientEntity?> GetClientByIdAsync(string clientId)
    {
        return await clientRepository.GetByIdAsync(clientId);
    }

    public async Task<ClientEntity?> GetClientByHostnameAsync(string hostname)
    {
        var computerInfo = await clientComputerInfoRepository.GetByHostnameAsync(hostname);
        if (computerInfo == null)
        {
            return null;
        }

        return await clientRepository.GetByIdAsync(computerInfo.ClientId);
    }

    public async Task<IEnumerable<ClientEntity>> GetAllClientsAsync(string? groupId = null, bool? isBlocked = null, string? search = null)
    {
        return await clientRepository.GetAllAsync(groupId, isBlocked, search);
    }

    public async Task<ClientDetailResponseDto> GetClientDetailAsync(string clientId)
    {
        var client = await clientRepository.GetByIdWithDetailsAsync(clientId) ?? throw new ArgumentException("Client not found");
        var response = mapper.Map<ClientDetailResponseDto>(client);

        // Добавляем историю
        var history = await clientHistoryRepository.GetByClientIdAsync(clientId, 50);
        response.History = mapper.Map<List<ClientHistoryResponseDto>>(history);

        return response;
    }

    public async Task<ClientEntity> CreateClientAsync(CreateClientRequestDto request)
    {
        // Проверяем, не существует ли уже клиент с таким ID
        var existing = await clientRepository.GetByIdAsync(request.ClientId);
        if (existing != null)
        {
            throw new InvalidOperationException($"Client with ID {request.ClientId} already exists");
        }

        var client = new ClientEntity
        {
            Id = request.ClientId,
            GroupId = request.GroupId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        client = await clientRepository.CreateAsync(client);

        // Создаём информацию о компьютере
        await clientComputerInfoRepository.CreateAsync(new ClientComputerInfoEntity
        {
            ClientId = client.Id,
            Hostname = request.Name ?? "unknown",
            UpdatedAt = DateTime.UtcNow
        });

        await AddClientHistoryAsync(client.Id, ClientChangeType.SessionCreated.ToString(), null, "Client created by admin", null);

        return client;
    }

    public async Task<ClientEntity> UpdateClientAsync(string clientId, UpdateClientRequestDto request)
    {
        var client = await clientRepository.GetByIdAsync(clientId) ?? throw new ArgumentException("Client not found");
        if (request.GroupId != null)
        {
            _ = await groupRepository.GetByIdAsync(request.GroupId) ?? throw new ArgumentException("Group not found");
            var oldGroupId = client.GroupId;
            client.GroupId = request.GroupId;
            client.UpdatedAt = DateTime.UtcNow;

            if (oldGroupId != request.GroupId)
            {
                await AddClientHistoryAsync(client.Id, ClientChangeType.GroupChanged.ToString(), oldGroupId, request.GroupId, null);
            }
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            var computerInfo = await clientComputerInfoRepository.GetByClientIdAsync(clientId);
            if (computerInfo != null)
            {
                var oldHostname = computerInfo.Hostname;
                computerInfo.Hostname = request.Name;
                computerInfo.UpdatedAt = DateTime.UtcNow;
                await clientComputerInfoRepository.UpdateAsync(computerInfo);

                if (oldHostname != request.Name)
                {
                    await AddClientHistoryAsync(client.Id, ClientChangeType.HostnameChanged.ToString(), oldHostname, request.Name, null);
                }
            }
        }

        await clientRepository.UpdateAsync(client);

        return client;
    }

    public async Task DeleteClientAsync(string clientId)
    {
        var client = await clientRepository.GetByIdAsync(clientId) ?? throw new ArgumentException("Client not found");
        client.IsActive = false;
        client.UpdatedAt = DateTime.UtcNow;
        await clientRepository.UpdateAsync(client);
    }

    public async Task BlockClientAsync(string clientId, string reason, string blockedBy)
    {
        var client = await clientRepository.GetByIdAsync(clientId) ?? throw new ArgumentException("Client not found");
        client.IsBlocked = true;
        client.UpdatedAt = DateTime.UtcNow;
        await clientRepository.UpdateAsync(client);

        await clientBlockHistoryRepository.CreateAsync(new ClientBlockHistoryEntity
        {
            ClientId = clientId,
            Action = "blocked",
            Reason = reason,
            BlockedBy = blockedBy,
            CreatedAt = DateTime.UtcNow
        });

        await AddClientHistoryAsync(clientId, ClientChangeType.Blocked.ToString(), null, reason, null);
    }

    public async Task UnblockClientAsync(string clientId)
    {
        var client = await clientRepository.GetByIdAsync(clientId) ?? throw new ArgumentException("Client not found");
        client.IsBlocked = false;
        client.UpdatedAt = DateTime.UtcNow;
        await clientRepository.UpdateAsync(client);

        await clientBlockHistoryRepository.CreateAsync(new ClientBlockHistoryEntity
        {
            ClientId = clientId,
            Action = "unblocked",
            CreatedAt = DateTime.UtcNow
        });

        await AddClientHistoryAsync(clientId, ClientChangeType.Unblocked.ToString(), null, null, null);
    }

    public async Task UpdateClientNetworkInfoAsync(string clientId, string ipAddress, string? macAddress = null, string? networkInterface = null)
    {
        var client = await clientRepository.GetByIdAsync(clientId);
        if (client == null)
        {
            return;
        }

        // Проверяем, есть ли уже запись с таким IP
        var existing = await clientNetworkInfoRepository.GetByClientAndIpAsync(clientId, ipAddress);
        if (existing != null)
        {
            existing.LastSeen = DateTime.UtcNow;
            existing.IsActive = true;
            await clientNetworkInfoRepository.UpdateAsync(existing);

            // Если изменился MAC или интерфейс
            if (existing.MacAddress != macAddress)
            {
                await AddClientHistoryAsync(clientId, ClientChangeType.MacAddressChanged.ToString(), existing.MacAddress, macAddress, null);
            }
            if (existing.NetworkInterface != networkInterface)
            {
                await AddClientHistoryAsync(clientId, ClientChangeType.NetworkInterfaceChanged.ToString(), existing.NetworkInterface, networkInterface, null);
            }
        }
        else
        {
            // Создаём новую запись
            await clientNetworkInfoRepository.CreateAsync(new ClientNetworkInfoEntity
            {
                ClientId = clientId,
                IpAddress = ipAddress,
                MacAddress = macAddress,
                NetworkInterface = networkInterface,
                LastSeen = DateTime.UtcNow,
                IsActive = true
            });

            // Добавляем в историю изменение IP
            await AddClientHistoryAsync(clientId, ClientChangeType.IpChanged.ToString(), null, ipAddress, null);
        }

        // Деактивируем старые записи с другим IP
        var oldRecords = await clientNetworkInfoRepository.GetByClientIdAsync(clientId);
        foreach (var record in oldRecords.Where(r => r.IpAddress != ipAddress && r.IsActive))
        {
            record.IsActive = false;
            await clientNetworkInfoRepository.UpdateAsync(record);
        }
    }

    public async Task UpdateClientComputerInfoAsync(string clientId, ClientInfoDto clientInfo)
    {
        var computerInfo = await clientComputerInfoRepository.GetByClientIdAsync(clientId);
        if (computerInfo == null)
        {
            return;
        }

        bool changed = false;

        if (!string.IsNullOrEmpty(clientInfo.Hostname) && computerInfo.Hostname != clientInfo.Hostname)
        {
            await AddClientHistoryAsync(clientId, ClientChangeType.HostnameChanged.ToString(), computerInfo.Hostname, clientInfo.Hostname, null);
            computerInfo.Hostname = clientInfo.Hostname;
            changed = true;
        }

        if (clientInfo.OsVersion != null && computerInfo.OsVersion != clientInfo.OsVersion)
        {
            await AddClientHistoryAsync(clientId, ClientChangeType.OsVersionChanged.ToString(), computerInfo.OsVersion, clientInfo.OsVersion, null);
            computerInfo.OsVersion = clientInfo.OsVersion;
            changed = true;
        }

        if (clientInfo.CpuInfo != null && computerInfo.CpuInfo != clientInfo.CpuInfo)
        {
            await AddClientHistoryAsync(clientId, ClientChangeType.CpuInfoChanged.ToString(), computerInfo.CpuInfo, clientInfo.CpuInfo, null);
            computerInfo.CpuInfo = clientInfo.CpuInfo;
            changed = true;
        }

        if (clientInfo.MemoryGb.HasValue && computerInfo.MemoryGb != clientInfo.MemoryGb)
        {
            await AddClientHistoryAsync(clientId, ClientChangeType.MemoryChanged.ToString(), computerInfo.MemoryGb?.ToString(), clientInfo.MemoryGb?.ToString(), null);
            computerInfo.MemoryGb = clientInfo.MemoryGb;
            changed = true;
        }

        if (clientInfo.DiskGb.HasValue && computerInfo.DiskGb != clientInfo.DiskGb)
        {
            await AddClientHistoryAsync(clientId, ClientChangeType.DiskChanged.ToString(), computerInfo.DiskGb?.ToString(), clientInfo.DiskGb?.ToString(), null);
            computerInfo.DiskGb = clientInfo.DiskGb;
            changed = true;
        }

        if (clientInfo.Architecture != null && computerInfo.Architecture != clientInfo.Architecture)
        {
            await AddClientHistoryAsync(clientId, ClientChangeType.ArchitectureChanged.ToString(), computerInfo.Architecture, clientInfo.Architecture, null);
            computerInfo.Architecture = clientInfo.Architecture;
            changed = true;
        }

        if (clientInfo.KernelVersion != null && computerInfo.KernelVersion != clientInfo.KernelVersion)
        {
            await AddClientHistoryAsync(clientId, ClientChangeType.KernelVersionChanged.ToString(), computerInfo.KernelVersion, clientInfo.KernelVersion, null);
            computerInfo.KernelVersion = clientInfo.KernelVersion;
            changed = true;
        }

        if (changed)
        {
            computerInfo.UpdatedAt = DateTime.UtcNow;
            await clientComputerInfoRepository.UpdateAsync(computerInfo);
        }
    }

    public async Task AddClientHistoryAsync(string clientId, string changeType, string? oldValue, string? newValue, int? requestId)
    {
        if (!Enum.TryParse<ClientChangeType>(changeType, true, out var changeTypeEnum))
        {
            changeTypeEnum = ClientChangeType.SessionCreated;
        }

        var history = new ClientHistoryEntity
        {
            ClientId = clientId,
            ChangeType = changeTypeEnum,
            OldValue = oldValue,
            NewValue = newValue,
            ChangeTimestamp = DateTime.UtcNow,
            RequestId = requestId
        };

        await clientHistoryRepository.CreateAsync(history);
    }
}