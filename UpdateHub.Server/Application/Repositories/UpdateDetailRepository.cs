using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class UpdateDetailRepository(AppDbContext context) : BaseRepository<UpdateDetailEntity>(context), IUpdateDetailRepository
{
    public async Task<IEnumerable<UpdateDetailEntity>> GetByUpdateRequestIdAsync(int updateRequestId)
    {
        return await _dbSet
            .Where(x => x.UpdateRequestId == updateRequestId)
            .ToListAsync();
    }
}