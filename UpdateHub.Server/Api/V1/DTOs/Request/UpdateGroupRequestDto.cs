namespace UpdateHub.Server.Api.V1.DTOs.Request;

/// <summary>Изменение группы компьютеров.</summary>
public class UpdateGroupRequestDto
{
    /// <summary>Новое название; <see langword="null"/> — не менять.</summary>
    public string? Name { get; set; }

    /// <summary>Новое описание; <see langword="null"/> — не менять.</summary>
    public string? Description { get; set; }
}
