namespace UpdateHub.Shared.Contracts;

/// <summary>
/// Список пользователей.
/// </summary>
/// <remarks>
/// Списки отдаются не голым массивом, а объектом с полем количества.
/// Так ответ можно расширить постраничной выдачей, не ломая тех, кто его уже
/// разбирает: массив пришлось бы заменить целиком.
/// <para>
/// Обобщённого типа здесь нет намеренно: имя поля со списком в каждом ответе
/// своё (<c>users</c>, <c>clients</c>), и панель читает его как есть, без
/// настройки сериализатора под каждый случай.
/// </para>
/// </remarks>
public class UserListResponseDto
{
    /// <summary>Пользователи.</summary>
    public List<UserResponseDto> Users { get; set; } = [];

    /// <summary>Общее количество.</summary>
    public int Total { get; set; }
}

/// <summary>Список компьютеров.</summary>
public class ClientListResponseDto
{
    /// <summary>Компьютеры.</summary>
    public List<ClientResponseDto> Clients { get; set; } = [];

    /// <summary>Общее количество.</summary>
    public int Total { get; set; }
}

/// <summary>Список групп.</summary>
public class GroupListResponseDto
{
    /// <summary>Группы.</summary>
    public List<GroupResponseDto> Groups { get; set; } = [];

    /// <summary>Общее количество.</summary>
    public int Total { get; set; }
}

/// <summary>Список заявок на регистрацию.</summary>
public class EnrollmentListResponseDto
{
    /// <summary>Заявки.</summary>
    public List<EnrollmentResponseDto> Enrollments { get; set; } = [];

    /// <summary>Общее количество.</summary>
    public int Total { get; set; }
}

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
