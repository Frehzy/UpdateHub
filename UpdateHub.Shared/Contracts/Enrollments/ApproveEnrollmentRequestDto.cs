namespace UpdateHub.Shared.Contracts.Enrollments;

/// <summary>Одобрение заявки на регистрацию компьютера.</summary>
public class ApproveEnrollmentRequestDto
{
    /// <summary>Группа, в которую поместить компьютер; <see langword="null"/> — без группы.</summary>
    public string? GroupId { get; set; }
}
