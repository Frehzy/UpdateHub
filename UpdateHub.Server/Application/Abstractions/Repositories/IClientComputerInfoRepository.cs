using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IClientComputerInfoRepository : IRepository<ClientComputerInfoEntity>
{
    Task<ClientComputerInfoEntity?> GetByClientIdAsync(string clientId);
    Task<ClientComputerInfoEntity?> GetByHostnameAsync(string hostname);
}