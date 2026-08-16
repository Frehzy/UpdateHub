using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

/// <summary>Доступ к пофайловой детализации обращений.</summary>
public interface IUpdateDetailRepository : IRepository<UpdateDetailEntity, int>
{
    /// <summary>Возвращает детализацию конкретного обращения.</summary>
    /// <param name="updateRequestId">Идентификатор обращения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список записей.</returns>
    Task<IReadOnlyList<UpdateDetailEntity>> GetByRequestIdAsync(int updateRequestId, CancellationToken cancellationToken = default);

    /// <summary>Добавляет пачку записей одним сохранением.</summary>
    /// <param name="details">Добавляемые записи.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    Task AddRangeAsync(IReadOnlyCollection<UpdateDetailEntity> details, CancellationToken cancellationToken = default);
}
