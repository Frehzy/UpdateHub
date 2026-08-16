using System.Text;
using UpdateHub.Server.Domain.ValueObjects;

namespace UpdateHub.Server.Application.Manifest;

/// <summary>
/// Разбор и генерация манифеста в формате утилиты <c>md5sum</c>.
/// </summary>
/// <remarks>
/// Формат выбран ради bash-клиента: манифест своей папки он получает командой
/// <c>md5sum</c>, а целостность скачанного проверяет командой <c>md5sum -c</c>.
/// Ни разбора JSON, ни зависимости от <c>jq</c> не требуется.
/// <para>
/// Строка выглядит так: 32 шестнадцатеричных символа, два пробела, путь до конца строки.
/// Вариант с <c>*</c> вместо второго пробела (двоичный режим <c>md5sum</c>) тоже принимается.
/// </para>
/// </remarks>
public static class ManifestFormat
{
    /// <summary>Длина шестнадцатеричной записи MD5.</summary>
    private const int Md5HexLength = 32;

    /// <summary>
    /// Разбирает манифест, присланный клиентом.
    /// </summary>
    /// <param name="content">Текст манифеста в формате <c>md5sum</c>.</param>
    /// <param name="maxEntries">Предел числа записей; при превышении разбор прерывается.</param>
    /// <returns>Результат разбора: словарь «путь → MD5» и список замечаний.</returns>
    public static ManifestParseResult Parse(string? content, int maxEntries)
    {
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(content))
        {
            return new ManifestParseResult(entries, errors);
        }

        var lineNumber = 0;

        foreach (var rawLine in content.Split('\n'))
        {
            lineNumber++;
            var line = rawLine.TrimEnd('\r');

            // Пустые строки и комментарии клиента игнорируем молча.
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            if (entries.Count >= maxEntries)
            {
                errors.Add($"манифест длиннее допустимых {maxEntries} записей, разбор прерван");
                break;
            }

            if (!TryParseLine(line, out var hash, out var path, out var lineError))
            {
                errors.Add($"строка {lineNumber}: {lineError}");
                continue;
            }

            if (!RelativePath.TryCreate(path, out var relativePath, out var pathError))
            {
                errors.Add($"строка {lineNumber}: {pathError}");
                continue;
            }

            // Повторный путь — берём первое вхождение, остальные отмечаем.
            if (!entries.TryAdd(relativePath!.Value, hash!))
            {
                errors.Add($"строка {lineNumber}: путь '{relativePath.Value}' встречается повторно");
            }
        }

        return new ManifestParseResult(entries, errors);
    }

    /// <summary>
    /// Разбирает одну строку манифеста.
    /// </summary>
    /// <param name="line">Строка без завершающего перевода каретки.</param>
    /// <param name="hash">Контрольная сумма в нижнем регистре.</param>
    /// <param name="path">Путь файла в том виде, в каком он записан.</param>
    /// <param name="error">Причина отказа.</param>
    /// <returns><see langword="true"/>, если строка разобрана.</returns>
    private static bool TryParseLine(string line, out string? hash, out string? path, out string? error)
    {
        hash = null;
        path = null;
        error = null;

        // md5sum помечает строки с экранированными символами ведущим обратным слэшем.
        if (line[0] == '\\')
        {
            error = "экранированный путь не поддерживается";
            return false;
        }

        if (line.Length < Md5HexLength + 2)
        {
            error = "строка короче минимально возможной";
            return false;
        }

        var hashPart = line[..Md5HexLength];
        if (!IsHex(hashPart))
        {
            error = "первые 32 символа не являются шестнадцатеричным MD5";
            return false;
        }

        // Разделитель: два пробела (текстовый режим) либо пробел и звёздочка (двоичный).
        var separator = line.AsSpan(Md5HexLength, 2);
        if (separator[0] != ' ' || (separator[1] != ' ' && separator[1] != '*'))
        {
            error = "неверный разделитель между суммой и путём";
            return false;
        }

        var pathPart = line[(Md5HexLength + 2)..];
        if (pathPart.Length == 0)
        {
            error = "путь отсутствует";
            return false;
        }

        hash = hashPart.ToLowerInvariant();
        path = pathPart;
        return true;
    }

    /// <summary>
    /// Записывает одну строку манифеста в формате <c>md5sum</c>.
    /// </summary>
    /// <param name="builder">Приёмник текста.</param>
    /// <param name="md5Hash">Контрольная сумма файла.</param>
    /// <param name="relativePath">Путь относительно каталога раздачи.</param>
    public static void AppendLine(StringBuilder builder, string md5Hash, string relativePath)
    {
        builder.Append(md5Hash).Append("  ").Append(relativePath).Append('\n');
    }

    /// <summary>
    /// Проверяет, что строка состоит только из шестнадцатеричных цифр.
    /// </summary>
    /// <param name="value">Проверяемая строка.</param>
    /// <returns><see langword="true"/>, если строка шестнадцатеричная.</returns>
    private static bool IsHex(ReadOnlySpan<char> value)
    {
        foreach (var c in value)
        {
            var isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Результат разбора манифеста клиента.
/// </summary>
/// <param name="Entries">Разобранные записи: путь относительно корня — контрольная сумма.</param>
/// <param name="Errors">Замечания по строкам, которые не удалось разобрать.</param>
public sealed record ManifestParseResult(
    IReadOnlyDictionary<string, string> Entries,
    IReadOnlyList<string> Errors);
