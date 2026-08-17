using Microsoft.EntityFrameworkCore;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Clients;
using UpdateHub.BackendServer.Domain.Entities.Clients;
using UpdateHub.BackendServer.Infrastructure.Database;

namespace UpdateHub.BackendServer.Application.Repositories.Clients;

/// <summary>Доступ к истории изменений характеристик компьютеров.</summary>
/// <param name="context">Контекст базы данных.</param>
public class ClientHistoryRepository(AppDbContext context)
    : BaseRepository<ClientHistoryEntity, int>(context), IClientHistoryRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ClientHistoryEntity>> GetByClientIdAsync(
        string clientId,
        int limit = 50,
        CancellationToken cancellationToken = default)
        => await Set
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.ChangeTimestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// Удаление выполняется одним запросом. Прежняя версия выбирала все записи
    /// в память и удаляла их по одной, передавая числовой ключ строкой,
    /// из-за чего очистка падала на первой же записи.
    /// </remarks>
    public Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
        => Set.Where(x => x.ChangeTimestamp < cutoff).ExecuteDeleteAsync(cancellationToken);
}
