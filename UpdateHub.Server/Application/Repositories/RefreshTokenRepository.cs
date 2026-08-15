using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Domain.Entities;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

public class RefreshTokenRepository(AppDbContext context) : BaseRepository<RefreshTokenEntity>(context), IRefreshTokenRepository
{
    public async Task<RefreshTokenEntity?> GetByTokenAsync(string tokenHash)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.Token == tokenHash);
    }

    public async Task<IEnumerable<RefreshTokenEntity>> GetByUserIdAsync(string userId)
    {
        return await _dbSet.Where(x => x.UserId == userId).ToListAsync();
    }

    public async Task RevokeAllForUserAsync(string userId)
    {
        var tokens = await _dbSet.Where(x => x.UserId == userId && x.RevokedAt == null).ToListAsync();
        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
    }
}