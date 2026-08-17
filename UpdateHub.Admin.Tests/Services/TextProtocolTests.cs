using UpdateHub.Admin.Services;

namespace UpdateHub.Admin.Tests.Services;

/// <summary>
/// Проверяет разбор текстовых ответов клиентской части API.
/// </summary>
/// <remarks>
/// Формат «ключ=значение» выбран ради bash-скрипта, который читает его
/// командами <c>grep</c> и <c>cut</c>. Панель пользуется теми же адресами
/// входа, поэтому разбирает тот же формат — и обязана делать это так же
/// снисходительно: пустые строки, отсутствующий перевод строки в конце
/// и знаки равенства внутри значения встречаются в настоящих ответах.
/// </remarks>
public class TextProtocolTests
{
    /// <summary>Обычный ответ разбирается по строкам.</summary>
    [Fact]
    public void Parse_TypicalResponse_ReadsAllPairs()
    {
        var pairs = TextProtocol.Parse("access_token=abc\nrefresh_token=def\nrole=Admin\n");

        Assert.Equal("abc", pairs["access_token"]);
        Assert.Equal("def", pairs["refresh_token"]);
        Assert.Equal("Admin", pairs["role"]);
    }

    /// <summary>
    /// Знак равенства внутри значения не ломает разбор.
    /// </summary>
    /// <remarks>
    /// Токены кодируются base64url и знака равенства не содержат, но
    /// сообщения об ошибках пишет человек, и разделять строку по первому
    /// знаку — единственный надёжный способ.
    /// </remarks>
    [Fact]
    public void Parse_ValueWithEqualsSign_KeepsWholeValue()
    {
        var pairs = TextProtocol.Parse("error=Ожидалось a=b\n");

        Assert.Equal("Ожидалось a=b", pairs["error"]);
    }

    /// <summary>Пустые строки пропускаются.</summary>
    [Fact]
    public void Parse_EmptyLines_Skipped()
    {
        var pairs = TextProtocol.Parse("\n\nrole=Admin\n\n");

        Assert.Single(pairs);
        Assert.Equal("Admin", pairs["role"]);
    }

    /// <summary>Строка без знака равенства пропускается, а не роняет разбор.</summary>
    [Fact]
    public void Parse_LineWithoutSeparator_Ignored()
    {
        var pairs = TextProtocol.Parse("сообщение без разделителя\nrole=Admin\n");

        Assert.Single(pairs);
        Assert.Equal("Admin", pairs["role"]);
    }

    /// <summary>Пустое значение сохраняется как пустая строка.</summary>
    /// <remarks>
    /// Так приходит <c>client_id</c> при входе в панель: компьютер не указан,
    /// и поле остаётся пустым.
    /// </remarks>
    [Fact]
    public void Parse_EmptyValue_KeptAsEmptyString()
    {
        var pairs = TextProtocol.Parse("client_id=\nrole=Admin\n");

        Assert.Equal(string.Empty, pairs["client_id"]);
    }

    /// <summary>Пустой ответ даёт пустой набор, а не исключение.</summary>
    [Fact]
    public void Parse_EmptyResponse_ReturnsEmpty()
    {
        Assert.Empty(TextProtocol.Parse(string.Empty));
    }

    /// <summary>Сообщение об ошибке достаётся из ответа.</summary>
    [Fact]
    public void ExtractError_WithMessage_ReturnsIt()
    {
        var message = TextProtocol.ExtractError("error=Неверный логин или пароль\n", "запасной текст");

        Assert.Equal("Неверный логин или пароль", message);
    }

    /// <summary>Без сообщения возвращается запасной текст.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("status=ok\n")]
    [InlineData("error=\n")]
    public void ExtractError_WithoutMessage_ReturnsFallback(string text)
    {
        Assert.Equal("запасной текст", TextProtocol.ExtractError(text, "запасной текст"));
    }
}
