using UpdateHub.Server.Domain.ValueObjects;

namespace UpdateHub.Tests.Domain.ValueObjects;

/// <summary>
/// Проверяет правила допустимости относительного пути.
/// </summary>
/// <remarks>
/// Через этот тип проходят и пути из манифеста клиента, и пути, найденные
/// при обходе каталога раздачи. Он решает две разные задачи: не пустить
/// обращение за пределы каталога и не пустить в протокол имена, которые
/// разъедут разбор на стороне bash.
/// </remarks>
public class RelativePathTests
{
    /// <summary>Обычные пути принимаются без изменений.</summary>
    [Theory]
    [InlineData("file.txt")]
    [InlineData("dir/file.txt")]
    [InlineData("a/b/c/d.bin")]
    [InlineData("файл.txt")]
    [InlineData("dir/файл с пробелом.txt")]
    [InlineData("astra176.iso")]
    public void TryCreate_ДопустимыйПуть_Принимается(string path)
    {
        var ok = RelativePath.TryCreate(path, out var result, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(path, result!.Value);
    }

    /// <summary>
    /// Ведущие и завершающие слэши срезаются: <c>/dir/file/</c> и <c>dir/file</c>
    /// обозначают один и тот же файл, и в манифесте они должны совпасть.
    /// </summary>
    [Theory]
    [InlineData("/file.txt", "file.txt")]
    [InlineData("dir/file.txt/", "dir/file.txt")]
    [InlineData("/dir/file.txt/", "dir/file.txt")]
    public void TryCreate_КрайниеСлэши_Срезаются(string input, string expected)
    {
        var ok = RelativePath.TryCreate(input, out var result, out _);

        Assert.True(ok);
        Assert.Equal(expected, result!.Value);
    }

    /// <summary>
    /// Выход за пределы каталога отвергается. Это первый рубеж защиты:
    /// такой путь не должен доходить до обращения к файловой системе.
    /// </summary>
    [Theory]
    [InlineData("../secret")]
    [InlineData("dir/../../secret")]
    [InlineData("..")]
    [InlineData("dir/./file")]
    [InlineData("dir//file")]
    public void TryCreate_ТочкиИПустыеСегменты_Отвергаются(string path)
    {
        var ok = RelativePath.TryCreate(path, out var result, out var error);

        Assert.False(ok);
        Assert.Null(result);
        Assert.NotNull(error);
    }

    /// <summary>
    /// Обратный слэш отвергается. Дело не в безопасности, а в протоколе:
    /// <c>md5sum</c> экранирует такие имена и ставит в начало строки
    /// обратный слэш, после чего разбор на стороне клиента разъезжается.
    /// </summary>
    [Fact]
    public void TryCreate_ОбратныйСлэш_Отвергается()
    {
        var ok = RelativePath.TryCreate(@"dir\file.txt", out _, out var error);

        Assert.False(ok);
        Assert.Contains("обратный слэш", error!, StringComparison.Ordinal);
    }

    /// <summary>
    /// Переводы строки и управляющие символы отвергаются: одна запись манифеста
    /// обязана занимать ровно одну строку.
    /// </summary>
    [Theory]
    [InlineData("dir/file\nname.txt")]
    [InlineData("dir/file\rname.txt")]
    [InlineData("dir/file\tname.txt")]
    [InlineData("dir/file\0name.txt")]
    public void TryCreate_УправляющиеСимволы_Отвергаются(string path)
    {
        var ok = RelativePath.TryCreate(path, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    /// <summary>Пустое значение отвергается с понятной причиной.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    [InlineData("///")]
    public void TryCreate_ПустойПуть_Отвергается(string? path)
    {
        var ok = RelativePath.TryCreate(path, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    /// <summary>
    /// Метод <c>Create</c> бросает исключение с указанием пути и причины —
    /// сообщение попадает в журнал, и по нему должно быть понятно, что не так.
    /// </summary>
    [Fact]
    public void Create_НедопустимыйПуть_БросаетИсключениеСПричиной()
    {
        var exception = Assert.Throws<ArgumentException>(() => RelativePath.Create("../secret"));

        Assert.Contains("../secret", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Допустимый путь метод <c>Create</c> возвращает без исключения.</summary>
    [Fact]
    public void Create_ДопустимыйПуть_ВозвращаетЗначение()
    {
        var path = RelativePath.Create("dir/file.txt");

        Assert.Equal("dir/file.txt", path.Value);
    }

    /// <summary>Неявное приведение к строке даёт нормализованное значение.</summary>
    [Fact]
    public void НеявноеПриведение_ДаётНормализованнуюСтроку()
    {
        string value = RelativePath.Create("/dir/file.txt/");

        Assert.Equal("dir/file.txt", value);
    }

    /// <summary>
    /// Сравнение идёт по значению: два пути, записанных по-разному, но
    /// нормализующихся одинаково, должны считаться равными.
    /// </summary>
    [Fact]
    public void Равенство_СравниваетсяПоНормализованномуЗначению()
    {
        var first = RelativePath.Create("dir/file.txt");
        var second = RelativePath.Create("/dir/file.txt/");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    /// <summary>
    /// Регистр сохраняется. Различать <c>Doc.txt</c> и <c>doc.txt</c> обязательно:
    /// на ext4 у клиента это разные файлы, а схлопывать их здесь означало бы
    /// потерять один из них.
    /// </summary>
    [Fact]
    public void TryCreate_РегистрСохраняется()
    {
        RelativePath.TryCreate("Doc.txt", out var upper, out _);
        RelativePath.TryCreate("doc.txt", out var lower, out _);

        Assert.NotEqual(upper, lower);
    }
}
