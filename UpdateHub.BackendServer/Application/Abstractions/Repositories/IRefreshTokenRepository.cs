using UpdateHub.BackendServer.Domain.Entities;

namespace UpdateHub.BackendServer.Application.Abstractions.Repositories;

/// <summary>Доступ к выданным refresh-токенам.</summary>
public interface IRefreshTokenRepository : IRepository<RefreshTokenEntity, string>
{
    /// <summary>Ищет токен по его хэшу.</summary>
    /// <param name="tokenHash">SHA-256 от значения токена.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Запись либо <see langword="null"/>.</returns>
    Task<RefreshTokenEntity?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отзывает один токен по его хэшу.
    /// </summary>
    /// <param name="tokenHash">SHA-256 от значения токена.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Число отозванных токенов: единица либо ноль, если он уже был отозван.</returns>
    /// <remarks>
    /// Отзыв — изменение одного поля, поэтому выполняется одним запросом,
    /// без загрузки сущности. Прежний путь «прочитать, изменить, сохранить»
    /// конфликтовал с отслеживанием: прочитанная копия оказывалась
    /// отсоединённой, а экземпляр с тем же ключом уже отслеживался контекстом
    /// после выдачи токена.
    /// </remarks>
    Task<int> RevokeAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Отзывает все действующие токены пользователя.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Число отозванных токенов.</returns>
    Task<int> RevokeAllForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Удаляет истёкшие и отозванные токены.</summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Число удалённых записей.</returns>
    Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default);
}
