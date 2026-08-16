using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Repositories;

/// <summary>Доступ к сетевым адресам компьютеров.</summary>
public interface IClientNetworkInfoRepository : IRepository<ClientNetworkInfoEntity, string>
{
    /// <summary>Возвращает все известные адреса компьютера.</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список адресов.</returns>
    Task<IReadOnlyList<ClientNetworkInfoEntity>> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>Ищет запись по паре «компьютер и адрес».</summary>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="ipAddress">IP-адрес.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Запись либо <see langword="null"/>.</returns>
    Task<ClientNetworkInfoEntity?> GetByClientAndIpAsync(string clientId, string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>Снимает признак активности с адресов, не встречавшихся с указанного момента.</summary>
    /// <param name="cutoff">Граничный момент времени.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Число изменённых записей.</returns>
    Task<int> DeactivateOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
