using System.Text.RegularExpressions;

namespace UpdateHub.BackendServer.Domain.ValueObjects;

/// <summary>
/// Проверенный относительный путь файла внутри каталога раздачи.
/// </summary>
/// <remarks>
/// Отсекает то, что ломает либо протокол, либо клиента:
/// выход за пределы каталога (<c>..</c>), абсолютные пути, обратные слэши
/// и переводы строк. Последние два опасны именно из-за формата манифеста —
/// <c>md5sum</c> экранирует их и ставит <c>\</c> в начало строки, после чего
/// разбор на стороне bash разъезжается.
/// Пробелы и кириллица допустимы: в формате <c>md5sum</c> путь занимает всё
/// от двух пробелов до конца строки, поэтому проблем не создаёт.
/// </remarks>
public sealed partial record RelativePath
{
    /// <summary>Нормализованное значение пути с прямыми слэшами.</summary>
    public string Value { get; }

    private RelativePath(string value) => Value = value;

    /// <summary>
    /// Пытается создать проверенный путь.
    /// </summary>
    /// <param name="path">Исходный путь, возможно с обратными слэшами.</param>
    /// <param name="result">Результат разбора, если путь допустим.</param>
    /// <param name="error">Причина отказа, если путь недопустим.</param>
    /// <returns><see langword="true"/>, если путь допустим.</returns>
    public static bool TryCreate(string? path, out RelativePath? result, out string? error)
    {
        result = null;
        error = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "путь пуст";
            return false;
        }

        if (path.Contains('\\'))
        {
            error = "путь содержит обратный слэш";
            return false;
        }

        if (ControlCharacters().IsMatch(path))
        {
            error = "путь содержит управляющие символы или перевод строки";
            return false;
        }

        // Абсолютный путь отвергается, а не превращается в относительный
        // отбрасыванием ведущего слэша: срезать его молча — значит подменить
        // присланное клиентом другим значением и скрыть ошибку в его скрипте.
        if (path[0] == '/')
        {
            error = "путь должен быть относительным";
            return false;
        }

        var normalized = path.TrimEnd('/');
        if (normalized.Length == 0)
        {
            error = "путь пуст после нормализации";
            return false;
        }

        var segments = normalized.Split('/');
        if (segments.Any(s => s.Length == 0 || s == "." || s == ".."))
        {
            error = "путь содержит пустой сегмент, '.' или '..'";
            return false;
        }

        result = new RelativePath(normalized);
        return true;
    }

    /// <summary>
    /// Создаёт проверенный путь или бросает исключение.
    /// </summary>
    /// <param name="path">Исходный путь.</param>
    /// <returns>Проверенный путь.</returns>
    /// <exception cref="ArgumentException">Путь недопустим.</exception>
    public static RelativePath Create(string? path)
    {
        if (!TryCreate(path, out var result, out var error))
        {
            throw new ArgumentException($"Недопустимый путь '{path}': {error}", nameof(path));
        }

        return result!;
    }

    /// <summary>Возвращает строковое значение пути.</summary>
    public override string ToString() => Value;

    /// <summary>Неявное приведение к строке.</summary>
    /// <param name="path">Проверенный путь.</param>
    public static implicit operator string(RelativePath path) => path.Value;

    [GeneratedRegex(@"[\p{Cc}]")]
    private static partial Regex ControlCharacters();
}
