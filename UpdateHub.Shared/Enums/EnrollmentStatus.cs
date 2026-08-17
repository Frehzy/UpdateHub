namespace UpdateHub.Shared.Enums;

/// <summary>
/// Состояние заявки на регистрацию компьютера.
/// </summary>
public enum EnrollmentStatus
{
    /// <summary>Заявка подана и ждёт решения администратора.</summary>
    Pending,

    /// <summary>Заявка одобрена, компьютер заведён в системе.</summary>
    Approved,

    /// <summary>Заявка отклонена.</summary>
    Rejected
}
