namespace UpdateHub.BackendServer.Application.Maintenance;

/// <summary>
/// Состояние резервного копирования: чем закончились последние попытки.
/// </summary>
/// <remarks>
/// Заведено потому, что узнать о работе копирования было нельзя иначе как
/// заглянув в папку или прочитав журнал. Служба при неудаче не роняет сервер —
/// раздача файлов важнее копий, — и это верно, но означает, что отказавшее
/// копирование остаётся незамеченным. В контуре без интернета, куда нужно
/// ехать, журнал не читает никто, и молчащее копирование — обычный способ
/// однажды обнаружить, что восстанавливать нечем.
/// <para>
/// Состояние живёт в памяти, а не в базе, намеренно. Смысл копии — пережить
/// потерю базы; писать её состояние в ту же базу значило бы потерять его
/// ровно тогда, когда оно нужно. После перезапуска состояние пустует считанные
/// секунды: первая копия снимается сразу на старте.
/// </para>
/// </remarks>
public sealed class BackupState
{
    private volatile BackupAttempt? _last;
    private volatile BackupAttempt? _lastSuccess;
    private int _successCount;
    private int _failureCount;

    /// <summary>Последняя попытка, удачная или нет.</summary>
    public BackupAttempt? Last => _last;

    /// <summary>Последняя удачная попытка.</summary>
    /// <remarks>
    /// Отличается от <see cref="Last"/> тем, что отвечает на главный вопрос:
    /// когда в последний раз копия действительно получилась. Если попытки
    /// отказывают неделю, здесь останется копия недельной давности, и это
    /// именно то, что администратор должен увидеть.
    /// </remarks>
    public BackupAttempt? LastSuccess => _lastSuccess;

    /// <summary>Число удачных попыток с момента запуска.</summary>
    public int SuccessCount => Volatile.Read(ref _successCount);

    /// <summary>Число неудачных попыток с момента запуска.</summary>
    public int FailureCount => Volatile.Read(ref _failureCount);

    /// <summary>
    /// Отмечает удачно снятую копию.
    /// </summary>
    /// <param name="path">Путь к файлу копии.</param>
    /// <param name="sizeBytes">Размер файла в байтах.</param>
    public void Succeeded(string path, long sizeBytes)
    {
        var attempt = new BackupAttempt(
            At: DateTime.UtcNow,
            Succeeded: true,
            Path: path,
            SizeBytes: sizeBytes,
            Error: null);

        _last = attempt;
        _lastSuccess = attempt;
        Interlocked.Increment(ref _successCount);
    }

    /// <summary>
    /// Отмечает неудачную попытку.
    /// </summary>
    /// <param name="error">Краткое описание причины.</param>
    public void Failed(string error)
    {
        _last = new BackupAttempt(
            At: DateTime.UtcNow,
            Succeeded: false,
            Path: null,
            SizeBytes: 0,
            Error: error);
        Interlocked.Increment(ref _failureCount);
    }
}
