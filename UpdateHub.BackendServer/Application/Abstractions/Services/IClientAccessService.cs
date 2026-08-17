using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Domain.Entities;

namespace UpdateHub.BackendServer.Application.Abstractions.Services;

/// <summary>Проверка права пользователя работать за конкретным компьютером.</summary>
public interface IClientAccessService
{
    /// <summary>
    /// Проверяет, что компьютер известен, активен, не заблокирован
    /// и что у пользователя есть на него права.
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="isAdmin">Признак роли администратора.</param>
    /// <param name="clientId">Идентификатор компьютера.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат проверки с причиной отказа, если доступ не разрешён.</returns>
    Task<ClientAccessResult> AuthorizeAsync(
        string userId,
        bool isAdmin,
        string clientId,
        CancellationToken cancellationToken = default);

    /// <summary>Проверяет, есть ли у пользователя права хотя бы на один компьютер или группу.</summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns><see langword="true"/>, если хоть какие-то права выданы.</returns>
    Task<bool> HasAnyAccessAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат проверки доступа к компьютеру.
/// </summary>
/// <param name="Outcome">Исход проверки.</param>
/// <param name="Client">Компьютер, если он найден.</param>
/// <param name="Reason">Причина отказа для показа пользователю.</param>
public sealed record ClientAccessResult(ClientAccessOutcome Outcome, ClientEntity? Client, string? Reason)
{
    /// <summary>Доступ разрешён.</summary>
    public bool IsAllowed => Outcome == ClientAccessOutcome.Allowed;
}
