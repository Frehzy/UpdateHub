using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IClientRepository : IRepository<ClientEntity>
{
    Task<ClientEntity?> GetByIdWithDetailsAsync(string id);
    Task<IEnumerable<ClientEntity>> GetAllAsync(string? groupId = null, bool? isBlocked = null, string? search = null);
}