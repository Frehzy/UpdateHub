using UpdateHub.BackendServer.Domain.Entities;

namespace UpdateHub.BackendServer.Application.Abstractions.Repositories;

/// <summary>Доступ к группам компьютеров.</summary>
public interface IGroupRepository : IRepository<GroupEntity, string>
{
    /// <summary>Ищет активную группу по названию.</summary>
    /// <param name="name">Название группы.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Группа либо <see langword="null"/>.</returns>
    Task<GroupEntity?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Возвращает активные группы вместе с числом компьютеров.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список групп.</returns>
    Task<IReadOnlyList<GroupEntity>> GetActiveAsync(CancellationToken cancellationToken = default);
}
