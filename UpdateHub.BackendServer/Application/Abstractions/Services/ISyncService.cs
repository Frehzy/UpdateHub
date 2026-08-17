using UpdateHub.BackendServer.Application.Sync;

namespace UpdateHub.BackendServer.Application.Abstractions.Services;

/// <summary>Сравнение манифеста клиента с эталонным.</summary>
public interface ISyncService
{
    /// <summary>
    /// Сравнивает присланный клиентом манифест с эталонным и составляет план работ.
    /// </summary>
    /// <param name="request">Параметры сравнения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>План: что скачать и что на сервере отсутствует.</returns>
    Task<SyncPlan> BuildPlanAsync(SyncRequest request, CancellationToken cancellationToken = default);
}
