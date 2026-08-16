namespace UpdateHub.Server.Domain.Enums;

/// <summary>Характер изменения файла в каталоге раздачи.</summary>
public enum FileChangeType
{
    /// <summary>Файл появился.</summary>
    Created,

    /// <summary>Содержимое файла изменилось.</summary>
    Modified,

    /// <summary>Файл исчез.</summary>
    Deleted
}
