namespace UpdateHub.BackendServer.Application.Sync;

/// <summary>Исход проверки права работать за компьютером.</summary>
public enum ClientAccessOutcome
{
    /// <summary>Доступ разрешён.</summary>
    Allowed,

    /// <summary>
    /// Компьютер не зарегистрирован. Сервер намеренно не заводит его сам:
    /// пользователь должен подать заявку, а администратор — её рассмотреть.
    /// </summary>
    UnknownClient,

    /// <summary>Компьютер заблокирован администратором.</summary>
    Blocked,

    /// <summary>Компьютер известен, но прав на него у пользователя нет.</summary>
    Forbidden
}
