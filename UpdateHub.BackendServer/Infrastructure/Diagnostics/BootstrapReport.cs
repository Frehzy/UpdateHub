namespace UpdateHub.BackendServer.Infrastructure.Diagnostics;

/// <summary>
/// Сведения о первом запуске, которые нужно показать администратору.
/// </summary>
/// <remarks>
/// Подготовка базы идёт до того, как сервер начинает слушать порт, а сводка
/// печатается после — когда адреса уже назначены. Между этими моментами нужно
/// что-то, что переживёт оба, поэтому единственный экземпляр на приложение.
/// <para>
/// Прежде о созданном администраторе сообщала одна строка журнала посреди
/// подготовки базы. Найти её среди прочих строк было тем труднее, чем важнее
/// она была: без пароля в систему не войти, а узнать его повторно нельзя.
/// Теперь она попадает в ту же рамку, что и адреса сервера, — в конец вывода,
/// куда администратор и смотрит.
/// </para>
/// </remarks>
public sealed class BootstrapReport
{
    /// <summary>Была ли учётная запись администратора создана этим запуском.</summary>
    public bool AdminCreated { get; private set; }

    /// <summary>Логин созданного администратора.</summary>
    public string? Username { get; private set; }

    /// <summary>
    /// Пароль, сгенерированный сервером, либо <see langword="null"/>,
    /// если он был задан в настройках.
    /// </summary>
    /// <remarks>
    /// Заданный в настройках пароль не показывается: администратор его и так
    /// знает, а печатать в журнал то, что можно прочитать в файле, значит
    /// разносить его лишний раз.
    /// </remarks>
    public string? GeneratedPassword { get; private set; }

    /// <summary>
    /// Запоминает, что администратор создан на этом запуске.
    /// </summary>
    /// <param name="username">Логин.</param>
    /// <param name="generatedPassword">
    /// Сгенерированный пароль либо <see langword="null"/>, если он взят из настроек.
    /// </param>
    public void AdminWasCreated(string username, string? generatedPassword)
    {
        AdminCreated = true;
        Username = username;
        GeneratedPassword = generatedPassword;
    }
}
