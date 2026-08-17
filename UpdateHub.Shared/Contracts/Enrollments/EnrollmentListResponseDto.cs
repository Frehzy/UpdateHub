namespace UpdateHub.Shared.Contracts.Enrollments;

/// <summary>Список заявок на регистрацию.</summary>
public class EnrollmentListResponseDto
{
    /// <summary>Заявки.</summary>
    public List<EnrollmentResponseDto> Enrollments { get; set; } = [];

    /// <summary>Общее количество.</summary>
    public int Total { get; set; }
}
