using Microsoft.JSInterop;

namespace UpdateHub.Frontend.Tests.TestSupport;

/// <summary>
/// Подставное хранилище браузера.
/// </summary>
/// <remarks>
/// Панель обращается к <c>localStorage</c> напрямую через вызов JavaScript,
/// без промежуточной обёртки: собственная обёртка ради трёх операций — лишний
/// слой. Расплата за это здесь: в тестах приходится изображать выполнение
/// JavaScript. Заглушка понимает только три обращения, которыми пользуется
/// панель, и падает на любом другом — так опечатка в имени вызова обнаружится
/// сразу, а не молчаливым отсутствием сохранённого токена.
/// </remarks>
public sealed class FakeBrowserStorage : IJSRuntime
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <summary>Бросать ли исключение на любое обращение к хранилищу.</summary>
    /// <remarks>
    /// Так изображается браузер, в котором хранилище запрещено настройками.
    /// Панель обязана продолжить работу — просто вход не переживёт обновления
    /// страницы.
    /// </remarks>
    public bool IsBlocked { get; set; }

    /// <summary>Возвращает сохранённое значение.</summary>
    /// <param name="key">Ключ.</param>
    /// <returns>Значение или <c>null</c>.</returns>
    public string? Read(string key) => _values.GetValueOrDefault(key);

    /// <summary>Кладёт значение в хранилище до начала теста.</summary>
    /// <param name="key">Ключ.</param>
    /// <param name="value">Значение.</param>
    public void Seed(string key, string value) => _values[key] = value;

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
    {
        if (IsBlocked)
        {
            throw new JSException("хранилище запрещено настройками браузера");
        }

        var key = args is { Length: > 0 } ? args[0]?.ToString() ?? string.Empty : string.Empty;

        switch (identifier)
        {
            case "localStorage.getItem":
                var value = _values.GetValueOrDefault(key);
                return ValueTask.FromResult((TValue)(object?)value!);

            case "localStorage.setItem":
                _values[key] = args is { Length: > 1 } ? args[1]?.ToString() ?? string.Empty : string.Empty;
                return ValueTask.FromResult(default(TValue)!);

            case "localStorage.removeItem":
                _values.Remove(key);
                return ValueTask.FromResult(default(TValue)!);

            default:
                throw new InvalidOperationException($"Заглушка не знает вызова '{identifier}'");
        }
    }

    /// <inheritdoc />
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => InvokeAsync<TValue>(identifier, args);
}
