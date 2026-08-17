namespace UpdateHub.Shared.Contracts.Manifest;

/// <summary>
/// Итог внеочередного обхода каталога раздачи.
/// </summary>
public class RescanResultDto
{
    /// <summary>Признак успешного выполнения.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Сколько файлов найдено в каталоге.</summary>
    public int TotalFiles { get; set; }

    /// <summary>
    /// Для скольких файлов пришлось пересчитать контрольную сумму.
    /// </summary>
    /// <remarks>
    /// Сильно меньше общего числа: файлы с прежним размером и временем
    /// изменения не перечитываются. Иначе каждый обход перечитывал бы
    /// шестигигабайтный образ.
    /// </remarks>
    public int HashedFiles { get; set; }

    /// <summary>Сколько записей манифеста изменилось.</summary>
    public int Changes { get; set; }

    /// <summary>Пути, отклонённые при обходе.</summary>
    public IReadOnlyList<string> RejectedPaths { get; set; } = [];
}
