using Microsoft.EntityFrameworkCore;
using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Infrastructure.Database;

namespace UpdateHub.Server.Application.Repositories;

/// <summary>
/// Реализация базовых операций поверх EF Core.
/// </summary>
/// <typeparam name="TEntity">Тип сущности.</typeparam>
/// <typeparam name="TKey">Тип первичного ключа.</typeparam>
/// <param name="context">Контекст базы данных.</param>
public abstract class BaseRepository<TEntity, TKey>(AppDbContext context)
    : IRepository<TEntity, TKey> where TEntity : class
{
    /// <summary>Контекст базы данных.</summary>
    protected AppDbContext Context { get; } = context;

    /// <summary>Набор сущностей текущего типа.</summary>
    protected DbSet<TEntity> Set { get; } = context.Set<TEntity>();

    /// <inheritdoc />
    public virtual async Task<TEntity> CreateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        return await Set.FindAsync([id], cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await Set.ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Если сущность уже отслеживается контекстом, вызов <c>Update</c> не нужен
    /// и вреден — он помечает изменёнными все поля и цепляет граф навигационных
    /// свойств. Поэтому состояние проверяется явно.
    /// </remarks>
    public virtual async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        if (Context.Entry(entity).State == EntityState.Detached)
        {
            Set.Update(entity);
        }

        await Context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    /// <inheritdoc />
    public virtual async Task DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        Set.Remove(entity);
        await Context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Добавляет сущность в контекст без сохранения.
    /// Нужен для пакетной записи, когда один <c>SaveChanges</c> покрывает много строк.
    /// </summary>
    /// <param name="entity">Добавляемая сущность.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    public async Task AddWithoutSaveAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await Set.AddAsync(entity, cancellationToken);
    }

    /// <summary>Сохраняет все накопленные в контексте изменения.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Число затронутых строк.</returns>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Context.SaveChangesAsync(cancellationToken);
    }
}
