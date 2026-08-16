namespace UpdateHub.Server.Domain.Enums;

/// <summary>Роль учётной записи.</summary>
public enum UserRole
{
    /// <summary>Обычный пользователь: синхронизация на разрешённых ему компьютерах.</summary>
    Client,

    /// <summary>Администратор: полный доступ к панели управления.</summary>
    Admin
}
