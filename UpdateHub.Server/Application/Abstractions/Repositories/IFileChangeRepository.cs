using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

/// <summary>Доступ к истории изменений файлов каталога раздачи.</summary>
public interface IFileChangeRepository : IRepository<FileChangeEntity, int>
{
    /// <summary>Возвращает последние изменения файлов.</summary>
    /// <param name="limit">Максимальное число записей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список изменений от новых к старым.</returns>
    Task<IReadOnlyList<FileChangeEntity>> GetRecentAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>Добавляет пачку записей одним сохранением.</summary>
    /// <param name="changes">Добавляемые записи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task AddRangeAsync(IReadOnlyCollection<FileChangeEntity> changes, CancellationToken cancellationToken = default);

    /// <summary>Удаляет записи старше указанного момента.</summary>
    /// <param name="cutoff">Граничный момент времени.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Число удалённых записей.</returns>
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
