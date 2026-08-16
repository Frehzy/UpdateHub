namespace UpdateHub.Server.Application.Sync;

/// <summary>
/// Запрошенная сущность не найдена. Обработчик исключений превращает это в 404.
/// </summary>
/// <param name="message">Сообщение для клиента.</param>
public class EntityNotFoundException(string message) : Exception(message);

/// <summary>
/// Вход не выполнен: неверные учётные данные, отключённая запись
/// либо отсутствие прав. Обработчик исключений превращает это в 401.
/// </summary>
/// <param name="message">Сообщение для клиента.</param>
public class AuthenticationFailedException(string message) : Exception(message);

/// <summary>
/// Действие запрещено при текущих правах. Обработчик исключений превращает это в 403.
/// </summary>
/// <param name="message">Сообщение для клиента.</param>
public class AccessDeniedException(string message) : Exception(message);
