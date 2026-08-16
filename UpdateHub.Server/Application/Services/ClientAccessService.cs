using UpdateHub.Server.Application.Abstractions.Repositories;
using UpdateHub.Server.Application.Abstractions.Services;
using UpdateHub.Server.Application.Sync;

namespace UpdateHub.Server.Application.Services;

/// <summary>Проверка права пользователя работать за конкретным компьютером.</summary>
/// <param name="clientRepository">Доступ к компьютерам.</param>
/// <param name="userClientAccessRepository">Доступ к персональным разрешениям.</param>
/// <param name="userGroupAccessRepository">Доступ к разрешениям на группы.</param>
/// <param name="blockHistoryRepository">Доступ к истории блокировок.</param>
/// <param name="logger">Журнал.</param>
/// <remarks>
/// Единственное место, где принимается решение о допуске клиента.
/// Прежде проверка жила в middleware, который пытался достать идентификатор
/// компьютера из тела запроса по имени поля, которого там никогда не было,
/// и поэтому отклонял любое обращение обычного пользователя.
/// </remarks>
public class ClientAccessService(
    IClientRepository clientRepository,
    IUserClientAccessRepository userClientAccessRepository,
    IUserGroupAccessRepository userGroupAccessRepository,
    IClientBlockHistoryRepository blockHistoryRepository,
    ILogger<ClientAccessService> logger) : IClientAccessService
{
    /// <inheritdoc />
    public async Task<ClientAccessResult> AuthorizeAsync(
        string userId,
        bool isAdmin,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var client = await clientRepository.GetActiveWithInfoAsync(clientId, cancellationToken);

        if (client is null)
        {
            logger.LogWarning("Обращение с неизвестным компьютером {ClientId} от пользователя {UserId}", clientId, userId);
            return new ClientAccessResult(
                ClientAccessOutcome.UnknownClient,
                null,
                "Компьютер не зарегистрирован. Подайте заявку командой enroll и обратитесь к администратору");
        }

        if (client.IsBlocked)
        {
            var reason = await blockHistoryRepository.GetLatestBlockReasonAsync(clientId, cancellationToken);
            logger.LogWarning("Обращение с заблокированного компьютера {ClientId}", clientId);
            return new ClientAccessResult(
                ClientAccessOutcome.Blocked,
                client,
                string.IsNullOrWhiteSpace(reason)
                    ? "Компьютер заблокирован администратором"
                    : $"Компьютер заблокирован: {reason}");
        }

        if (isAdmin)
        {
            return new ClientAccessResult(ClientAccessOutcome.Allowed, client, null);
        }

        if (await userClientAccessRepository.ExistsAsync(userId, clientId, cancellationToken))
        {
            return new ClientAccessResult(ClientAccessOutcome.Allowed, client, null);
        }

        if (client.GroupId is not null &&
            await userGroupAccessRepository.ExistsAsync(userId, client.GroupId, cancellationToken))
        {
            return new ClientAccessResult(ClientAccessOutcome.Allowed, client, null);
        }

        logger.LogWarning("Пользователь {UserId} не имеет прав на компьютер {ClientId}", userId, clientId);
        return new ClientAccessResult(
            ClientAccessOutcome.Forbidden,
            client,
            "У вас нет прав на работу за этим компьютером. Обратитесь к администратору");
    }

    /// <inheritdoc />
    public async Task<bool> HasAnyAccessAsync(string userId, CancellationToken cancellationToken = default)
    {
        var clientAccesses = await userClientAccessRepository.GetByUserIdAsync(userId, cancellationToken);
        if (clientAccesses.Count > 0)
        {
            return true;
        }

        var groupAccesses = await userGroupAccessRepository.GetByUserIdAsync(userId, cancellationToken);
        return groupAccesses.Count > 0;
    }
}
