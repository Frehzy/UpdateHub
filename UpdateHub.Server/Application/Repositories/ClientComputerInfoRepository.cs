using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class ClientComputerInfoRepository(AppDbContext context) : BaseRepository<ClientComputerInfoEntity>(context), IClientComputerInfoRepository
{
    public async Task<ClientComputerInfoEntity?> GetByClientIdAsync(string clientId)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.ClientId == clientId);
    }

    public async Task<ClientComputerInfoEntity?> GetByHostnameAsync(string hostname)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.Hostname == hostname);
    }
}