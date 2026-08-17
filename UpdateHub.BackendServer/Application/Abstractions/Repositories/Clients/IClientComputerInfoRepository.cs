using UpdateHub.BackendServer.Domain.Entities.Clients;

namespace UpdateHub.BackendServer.Application.Abstractions.Repositories.Clients;

/// <summary>Доступ к сведениям о железе компьютеров.</summary>
public interface IClientComputerInfoRepository : IRepository<ClientComputerInfoEntity, string>
{
    /// <summary>Возвращает сведения о железе конкретного компьютера.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сведения либо <see langword="null"/>.</returns>
    Task<ClientComputerInfoEntity?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ищет компьютеры с указанным отпечатком железа.
    /// Помогает узнать машину, у которой сменился идентификатор после переустановки системы.
    /// </summary>
    /// <param name="fingerprint">Отпечаток железа.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список найденных сведений.</returns>
    Task<IReadOnlyList<ClientComputerInfoEntity>> GetByFingerprintAsync(string fingerprint, CancellationToken cancellationToken = default);
}
