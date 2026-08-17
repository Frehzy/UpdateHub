namespace UpdateHub.BackendServer.Domain.Entities.Clients;

/// <summary>
/// Сведения о железе и операционной системе компьютера.
/// Обновляются при каждом обращении клиента; изменения попадают
/// в <see cref="ClientHistoryEntity"/>.
/// </summary>
public class ClientComputerInfoEntity
{
    /// <summary>Первичный ключ.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Компьютер, к которому относятся сведения.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Сетевое имя компьютера.</summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// Отпечаток железа — хэш от серийных номеров, MAC-адресов и модели.
    /// Используется как подсказка администратору, а не как идентификатор:
    /// после переустановки системы <see cref="ClientId"/> меняется,
    /// а отпечаток позволяет узнать ту же машину.
    /// </summary>
    public string? HardwareFingerprint { get; set; }

    /// <summary>Версия операционной системы.</summary>
    public string? OsVersion { get; set; }

    /// <summary>Модель процессора.</summary>
    public string? CpuInfo { get; set; }

    /// <summary>Объём оперативной памяти в гигабайтах.</summary>
    public int? MemoryGb { get; set; }

    /// <summary>Объём диска в гигабайтах.</summary>
    public int? DiskGb { get; set; }

    /// <summary>Архитектура процессора, например <c>x86_64</c>.</summary>
    public string? Architecture { get; set; }

    /// <summary>Версия ядра Linux.</summary>
    public string? KernelVersion { get; set; }

    /// <summary>Момент последнего обновления сведений.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Компьютер (навигационное свойство).</summary>
    public ClientEntity? Client { get; set; }
}
