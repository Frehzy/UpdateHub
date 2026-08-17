namespace UpdateHub.BackendServer.Domain.Enums;

/// <summary>Итог сравнения манифеста клиента с эталонным.</summary>
public enum UpdateStatus
{
    /// <summary>Расхождений нет, скачивать нечего.</summary>
    Ok,

    /// <summary>Есть файлы, которые клиенту нужно скачать.</summary>
    Update
}
