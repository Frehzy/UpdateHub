namespace UpdateHub.BackendServer.Application.Maintenance;

/// <summary>
/// Итог одной попытки снять резервную копию.
/// </summary>
/// <param name="At">Момент попытки.</param>
/// <param name="Succeeded">Удалась ли попытка.</param>
/// <param name="Path">Путь к созданному файлу; пусто при неудаче.</param>
/// <param name="SizeBytes">Размер файла в байтах; ноль при неудаче.</param>
/// <param name="Error">Причина неудачи; пусто при успехе.</param>
/// <remarks>
/// Отдельная запись, а не набор полей в состоянии: она заменяется целиком
/// одним присваиванием ссылки, поэтому читающая сторона никогда не увидит
/// смесь из двух попыток — время от одной, размер от другой.
/// </remarks>
public sealed record BackupAttempt(
    DateTime At,
    bool Succeeded,
    string? Path,
    long SizeBytes,
    string? Error);
