using UpdateHub.Server.Api.V1.DTOs.Response;
using UpdateHub.Server.Application.Sync;
using UpdateHub.Server.Domain.Entities;

namespace UpdateHub.Server.Application.Abstractions.Services;

/// <summary>Журналирование обращений и сводная статистика.</summary>
public interface IStatisticsService
{
    /// <summary>
    /// Считает сводную статистику за период.
    /// </summary>
    /// <param name="days">Глубина периода в сутках; <see langword="null"/> — за всё время.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сводка.</returns>
    Task<StatsResponseDto> GetStatisticsAsync(int? days, CancellationToken cancellationToken = default);

    /// <summary>
    /// Записывает обращение клиента вместе с пофайловой детализацией.
    /// </summary>
    /// <param name="plan">План синхронизации, отданный клиенту.</param>
    /// <param name="request">Исходный запрос.</param>
    /// <param name="responseTimeMs">Время подготовки ответа в миллисекундах.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Созданная запись журнала.</returns>
    /// <remarks>
    /// Единственное место, где создаётся <see cref="UpdateRequestEntity"/>.
    /// Раньше такую же запись создавал ещё и сервис сравнения манифестов,
    /// из-за чего вся статистика удваивалась.
    /// </remarks>
    Task<UpdateRequestEntity> LogSyncAsync(
        SyncPlan plan,
        SyncRequest request,
        int responseTimeMs,
        CancellationToken cancellationToken = default);
}
