using UpdateHub.Server.Api.V1.DTOs.Request;
using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Services;

public interface IClientService
{
    Task<ClientEntity> GetOrCreateClientAsync(ClientInfoDto clientInfo);
    Task<ClientEntity?> GetClientByIdAsync(string clientId);
    Task<ClientEntity?> GetClientByHostnameAsync(string hostname);
    Task<IEnumerable<ClientEntity>> GetAllClientsAsync(string? groupId = null, bool? isBlocked = null, string? search = null);
    Task<ClientDetailResponseDto> GetClientDetailAsync(string clientId);
    Task<ClientEntity> CreateClientAsync(CreateClientRequestDto request);
    Task<ClientEntity> UpdateClientAsync(string clientId, UpdateClientRequestDto request);
    Task DeleteClientAsync(string clientId);
    Task BlockClientAsync(string clientId, string reason, string blockedBy);
    Task UnblockClientAsync(string clientId);
    Task UpdateClientNetworkInfoAsync(string clientId, string ipAddress, string? macAddress = null, string? networkInterface = null);
    Task UpdateClientComputerInfoAsync(string clientId, ClientInfoDto clientInfo);
    Task AddClientHistoryAsync(string clientId, string changeType, string? oldValue, string? newValue, int? requestId = null);
}