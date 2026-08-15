using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

public interface IRefreshTokenRepository : IRepository<RefreshTokenEntity>
{
    Task<RefreshTokenEntity?> GetByTokenAsync(string tokenHash);
    Task<IEnumerable<RefreshTokenEntity>> GetByUserIdAsync(string userId);
    Task RevokeAllForUserAsync(string userId);
}