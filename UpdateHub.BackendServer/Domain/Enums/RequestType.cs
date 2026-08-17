namespace UpdateHub.BackendServer.Domain.Enums;

/// <summary>Тип обращения клиента к серверу обновлений.</summary>
public enum RequestType
{
    /// <summary>Сравнение манифестов без намерения скачивать.</summary>
    Check,

    /// <summary>Сравнение манифестов перед скачиванием файлов.</summary>
    Sync
}
