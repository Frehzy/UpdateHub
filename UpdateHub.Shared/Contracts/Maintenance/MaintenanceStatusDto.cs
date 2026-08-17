namespace UpdateHub.Shared.Contracts.Maintenance;

/// <summary>
/// Состояние обслуживания сервера: резервные копии и место на дисках.
/// </summary>
/// <remarks>
/// Отдаётся только администратору. Обычному пользователю знать, сколько места
/// на диске сервера, незачем — он приходит за файлами, а не за состоянием
/// хозяйства.
/// </remarks>
public class MaintenanceStatusDto
{
    /// <summary>Когда в последний раз копия действительно получилась.</summary>
    /// <remarks>
    /// Пусто, если удачных копий с момента запуска не было. Это главное поле:
    /// если попытки отказывают неделю, здесь останется копия недельной давности.
    /// </remarks>
    public DateTime? LastSuccessAt { get; set; }

    /// <summary>Размер последней удачной копии в байтах.</summary>
    public long LastSuccessSizeBytes { get; set; }

    /// <summary>Путь к последней удачной копии.</summary>
    public string? LastSuccessPath { get; set; }

    /// <summary>Момент последней попытки, удачной или нет.</summary>
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>Удалась ли последняя попытка.</summary>
    public bool LastAttemptSucceeded { get; set; }

    /// <summary>Причина неудачи последней попытки, если она не удалась.</summary>
    public string? LastAttemptError { get; set; }

    /// <summary>Число удачных попыток с момента запуска.</summary>
    public int SuccessCount { get; set; }

    /// <summary>Число неудачных попыток с момента запуска.</summary>
    public int FailureCount { get; set; }

    /// <summary>Сколько файлов копий лежит в каталоге прямо сейчас.</summary>
    /// <remarks>
    /// Считается по каталогу, а не по счётчику попыток: это единственное число,
    /// которое переживает перезапуск сервера и показывает настоящий запас.
    /// </remarks>
    public int BackupFilesOnDisk { get; set; }

    /// <summary>Каталог резервных копий.</summary>
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>Период снятия копий в часах; ноль — копирование отключено.</summary>
    public int IntervalHours { get; set; }

    /// <summary>Сколько последних копий хранится.</summary>
    public int KeepCount { get; set; }

    /// <summary>Свободно на разделе с копиями, байт.</summary>
    public long? BackupFreeBytes { get; set; }

    /// <summary>Всего на разделе с копиями, байт.</summary>
    public long? BackupTotalBytes { get; set; }

    /// <summary>Свободно на разделе с файлами раздачи, байт.</summary>
    public long? FilesFreeBytes { get; set; }

    /// <summary>Всего на разделе с файлами раздачи, байт.</summary>
    public long? FilesTotalBytes { get; set; }
}
