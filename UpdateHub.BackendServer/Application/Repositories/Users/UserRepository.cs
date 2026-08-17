using Microsoft.EntityFrameworkCore;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Users;
using UpdateHub.BackendServer.Domain.Entities.Users;
using UpdateHub.BackendServer.Infrastructure.Database;
using UpdateHub.Shared.Enums;

namespace UpdateHub.BackendServer.Application.Repositories.Users;

/// <summary>Доступ к учётным записям пользователей.</summary>
/// <param name="context">Контекст базы данных.</param>
public class UserRepository(AppDbContext context)
    : BaseRepository<UserEntity, string>(context), IUserRepository
{
    /// <inheritdoc />
    public Task<UserEntity?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => Set.FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Сравнение идёт по значению перечисления, а не по результату <c>ToString()</c>:
    /// EF Core хранит роль строкой через преобразователь и умеет сравнивать её сам.
    /// </remarks>
    public async Task<IReadOnlyList<UserEntity>> GetByRoleAsync(UserRole role, CancellationToken cancellationToken = default)
        => await Set.Where(x => x.Role == role && x.IsActive).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
        => !await Set.AnyAsync(cancellationToken);

    /// <inheritdoc />
    public Task<UserEntity?> GetByIdWithAccessAsync(string userId, CancellationToken cancellationToken = default)
        => Set
            .Include(u => u.UserClientAccesses).ThenInclude(a => a.Client).ThenInclude(c => c!.ComputerInfo)
            .Include(u => u.UserGroupAccesses).ThenInclude(a => a.Group)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
}
