namespace UpdateHub.Shared.Contracts;

/// <summary>Включение и отключение учётной записи.</summary>
public class ToggleUserStatusRequestDto
{
    /// <summary>
    /// Новое состояние учётной записи. При отключении все выданные
    /// пользователю refresh-токены отзываются.
    /// </summary>
    public bool IsActive { get; set; }
}
