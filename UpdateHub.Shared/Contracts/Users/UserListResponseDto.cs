namespace UpdateHub.Shared.Contracts.Users;

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
