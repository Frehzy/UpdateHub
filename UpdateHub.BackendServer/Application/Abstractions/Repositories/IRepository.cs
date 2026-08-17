namespace UpdateHub.BackendServer.Application.Abstractions.Repositories;

/// <summary>
/// Базовые операции над сущностью.
/// </summary>
/// <typeparam name="TEntity">Тип сущности.</typeparam>
/// <typeparam name="TKey">
/// Тип первичного ключа. Вынесен в параметр намеренно: прежняя версия принимала
/// только <see cref="string"/>, и удаление сущностей с числовым ключом падало
/// с ошибкой несовпадения типов внутри EF Core.
/// </typeparam>
public interface IRepository<TEntity, TKey> where TEntity : class
{
    /// <summary>Добавляет сущность и сохраняет изменения.</summary>
    /// <param name="entity">Сохраняемая сущность.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сохранённая сущность.</returns>
    Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Возвращает сущность по первичному ключу.</summary>
    /// <param name="id">Значение ключа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сущность либо <see langword="null"/>.</returns>
    Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>Возвращает все сущности.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список сущностей.</returns>
    Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Сохраняет изменения сущности.</summary>
    /// <param name="entity">Изменённая сущность.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сохранённая сущность.</returns>
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>Удаляет сущность по первичному ключу.</summary>
    /// <param name="id">Значение ключа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task DeleteAsync(TKey id, CancellationToken cancellationToken = default);
}
