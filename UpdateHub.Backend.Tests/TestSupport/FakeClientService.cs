using UpdateHub.BackendServer.Application.Abstractions.Services.Clients;
using UpdateHub.BackendServer.Application.Services.Clients;
using UpdateHub.BackendServer.Application.Sync;
using UpdateHub.BackendServer.Domain.Entities.Clients;
using UpdateHub.BackendServer.Domain.Enums;
using UpdateHub.Shared.Contracts.Clients;

namespace UpdateHub.Backend.Tests.TestSupport;

/// <summary>
/// Заглушка управления компьютерами для тестов службы авторизации.
/// </summary>
/// <remarks>
/// Настоящий <c>ClientService</c> требует настроенный AutoMapper, который
/// к проверяемому здесь поведению отношения не имеет. Служба авторизации
/// пользуется только записью истории, поэтому заглушка запоминает вызовы,
/// а остальные операции объявляет неподдерживаемыми: если тест случайно
/// зайдёт не туда, он упадёт с внятной ошибкой, а не тихо получит null.
/// </remarks>
public sealed class FakeClientService : IClientService
{
    /// <summary>Записи, добавленные в историю за время теста.</summary>
    public List<(string ClientId, ClientChangeType ChangeType, string? OldValue, string? NewValue)> History { get; } = [];

    /// <summary>Вызовы обновления сведений о компьютере.</summary>
    public List<(string ClientId, ClientReport Report)> CheckIns { get; } = [];

    /// <inheritdoc />
    public Task AddHistoryAsync(
        string clientId,
        ClientChangeType changeType,
        string? oldValue,
        string? newValue,
        CancellationToken cancellationToken = default)
    {
        History.Add((clientId, changeType, oldValue, newValue));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RecordCheckInAsync(
        string clientId,
        ClientReport report,
        ConnectionContext context,
        CancellationToken cancellationToken = default)
    {
        CheckIns.Add((clientId, report));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ClientEntity?> GetByIdAsync(string clientId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Заглушка не поддерживает эту операцию");

    /// <inheritdoc />
    public Task<IReadOnlyList<ClientEntity>> GetAllAsync(
        string? groupId,
        bool? isBlocked,
        string? search,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Заглушка не поддерживает эту операцию");

    /// <inheritdoc />
    public Task<ClientDetailResponseDto> GetDetailAsync(string clientId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Заглушка не поддерживает эту операцию");

    /// <inheritdoc />
    public Task<ClientEntity> CreateAsync(CreateClientRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Заглушка не поддерживает эту операцию");

    /// <inheritdoc />
    public Task<ClientEntity> UpdateAsync(string clientId, UpdateClientRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Заглушка не поддерживает эту операцию");

    /// <inheritdoc />
    public Task DeleteAsync(string clientId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Заглушка не поддерживает эту операцию");

    /// <inheritdoc />
    public Task BlockAsync(string clientId, string reason, string blockedBy, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Заглушка не поддерживает эту операцию");

    /// <inheritdoc />
    public Task UnblockAsync(string clientId, string unblockedBy, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Заглушка не поддерживает эту операцию");
}
