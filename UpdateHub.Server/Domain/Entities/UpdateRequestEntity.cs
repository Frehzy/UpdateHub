using UpdateHub.Server.Domain.Enums;

namespace UpdateHub.Server.Domain.Entities;

/// <summary>
/// Журнальная запись об обращении клиента за обновлением.
/// </summary>
/// <remarks>
/// Создаётся ровно один раз на запрос — в <c>StatisticsService</c>.
/// Прежняя версия писала такую же запись ещё и в сервисе синхронизации,
/// из-за чего вся статистика удваивалась.
/// </remarks>
public class UpdateRequestEntity
{
    /// <summary>Первичный ключ (автоинкремент).</summary>
    public int Id { get; set; }

    /// <summary>Компьютер, обратившийся за обновлением.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Логин пользователя, от имени которого выполнен запрос.</summary>
    public string? Username { get; set; }

    /// <summary>Момент обращения.</summary>
    public DateTime RequestTimestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Тип обращения.</summary>
    public RequestType RequestType { get; set; }

    /// <summary>Контрольная сумма присланного клиентом манифеста.</summary>
    public string? ClientManifestHash { get; set; }

    /// <summary>Итог сравнения: всё совпало либо требуется обновление.</summary>
    public UpdateStatus Status { get; set; }

    /// <summary>Сколько файлов клиенту предстоит скачать.</summary>
    public int FilesToUpdate { get; set; }

    /// <summary>Суммарный объём файлов к скачиванию в байтах.</summary>
    public long TotalSizeBytes { get; set; }

    /// <summary>Время подготовки ответа в миллисекундах.</summary>
    public int? ResponseTimeMs { get; set; }

    /// <summary>Компьютер (навигационное свойство).</summary>
    public ClientEntity? Client { get; set; }

    /// <summary>Пофайловая детализация выдачи.</summary>
    public ICollection<UpdateDetailEntity> UpdateDetails { get; set; } = [];
}
