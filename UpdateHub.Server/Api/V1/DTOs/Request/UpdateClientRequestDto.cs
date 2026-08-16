namespace UpdateHub.Server.Api.V1.DTOs.Request;

/// <summary>Изменение имени и группы компьютера.</summary>
public class UpdateClientRequestDto
{
    /// <summary>Новое отображаемое имя; <see langword="null"/> — не менять.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// Новая группа; <see langword="null"/> — не менять,
    /// пустая строка — убрать компьютер из группы.
    /// </summary>
    public string? GroupId { get; set; }
}
