using Microsoft.EntityFrameworkCore;
using UpdateHub.BackendServer.Application.Abstractions.Repositories.Updates;
using UpdateHub.BackendServer.Domain.Entities.Updates;
using UpdateHub.BackendServer.Infrastructure.Database;

namespace UpdateHub.BackendServer.Application.Repositories.Updates;

/// <summary>Доступ к журналу обращений клиентов.</summary>
/// <param name="context">Контекст базы данных.</param>
public class UpdateRequestRepository(AppDbContext context)
    : BaseRepository<UpdateRequestEntity, int>(context), IUpdateRequestRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<UpdateRequestEntity>> GetByClientIdAsync(
        string clientId,
        int limit = 50,
        CancellationToken cancellationToken = default)
        => await Set
            .Where(x => x.ClientId == clientId)
            .OrderByDescending(x => x.RequestTimestamp)
            .Take(limit)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<RequestSummary> GetSummaryAsync(DateTime? from, CancellationToken cancellationToken = default)
    {
        var query = Filtered(from);

        // Агрегаты считает SQLite; в память приходит одна строка.
        var aggregate = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Bytes = g.Sum(x => x.TotalSizeBytes)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var uniqueClients = await query.Select(x => x.ClientId).Distinct().CountAsync(cancellationToken);

        return new RequestSummary(aggregate?.Total ?? 0, uniqueClients, aggregate?.Bytes ?? 0);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<(DateTime Date, int Count)>> GetDailyCountsAsync(
        DateTime? from,
        CancellationToken cancellationToken = default)
    {
        // Сутки считаются по времени сервера, а не по UTC. Обращения хранятся
        // в UTC — так их и надо хранить, — но администратор смотрит на список
        // по местным часам: при смещении +7 всё, что произошло до семи утра,
        // попадало в предыдущую дату, и сводка не совпадала с рабочим днём.
        //
        // Смещение берётся одним значением на весь период. Для поясов без
        // перехода на летнее время это точно; в поясах с переходом сутки вокруг
        // самого перехода сдвинутся на час — цена, которую здесь платить не жаль.
        //
        // В контейнере пояс по умолчанию UTC. Чтобы «серверное время» означало
        // местное, контейнеру передаётся переменная TZ — см. Makefile.
        var offsetMinutes = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes;

        var rows = await Filtered(from)
            .GroupBy(x => x.RequestTimestamp.AddMinutes(offsetMinutes).Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(r => (r.Date, r.Count))];
    }

    /// <inheritdoc />
    public Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
        => Set.Where(x => x.RequestTimestamp < cutoff).ExecuteDeleteAsync(cancellationToken);

    /// <summary>Применяет ограничение по нижней границе периода.</summary>
    /// <param name="from">Нижняя граница либо <see langword="null"/>.</param>
    /// <returns>Запрос с наложенным фильтром.</returns>
    private IQueryable<UpdateRequestEntity> Filtered(DateTime? from)
        => from.HasValue ? Set.Where(x => x.RequestTimestamp >= from.Value) : Set;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, DateTime>> GetLastRequestPerClientAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await Set
            .GroupBy(request => request.ClientId)
            .Select(group => new { ClientId = group.Key, LastAt = group.Max(r => r.RequestTimestamp) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.ClientId, row => row.LastAt, StringComparer.Ordinal);
    }
}
