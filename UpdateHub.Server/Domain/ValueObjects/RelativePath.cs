namespace UpdateHub.Server.Domain.ValueObjects;

public record RelativePath
{
    public string Value { get; }

    public RelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty", nameof(path));

        // Нормализация пути: заменяем \ на /, убираем ведущие и завершающие /
        var normalized = path.Replace('\\', '/').Trim('/');
        if (string.IsNullOrEmpty(normalized))
            throw new ArgumentException("Invalid path", nameof(path));

        Value = normalized;
    }

    public static implicit operator string(RelativePath path) => path.Value;
    public static implicit operator RelativePath(string path) => new(path);

    public override string ToString() => Value;
}