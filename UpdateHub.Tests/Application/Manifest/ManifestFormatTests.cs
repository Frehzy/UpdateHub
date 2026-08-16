using UpdateHub.Server.Application.Manifest;

namespace UpdateHub.Tests.Application.Manifest;

/// <summary>
/// Проверяет разбор манифеста в формате утилиты <c>md5sum</c>.
/// </summary>
/// <remarks>
/// Это центральная точка протокола: сюда попадает всё, что присылает
/// bash-скрипт клиента. Ошибка разбора здесь означает либо неверный план
/// синхронизации, либо отказ обслуживать исправный клиент.
/// </remarks>
public class ManifestFormatTests
{
    /// <summary>Предел записей, заведомо не мешающий тестам.</summary>
    private const int NoLimit = 1000;

    /// <summary>
    /// Разбирает строку в том виде, в каком её выдаёт <c>md5sum</c>:
    /// сумма, два пробела, путь.
    /// </summary>
    [Fact]
    public void Parse_PlainLine_ReturnsPathAndHash()
    {
        var result = ManifestFormat.Parse("d41d8cd98f00b204e9800998ecf8427e  bin/app", NoLimit);

        Assert.Empty(result.Errors);
        Assert.Single(result.Entries);
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", result.Entries["bin/app"]);
    }

    /// <summary>
    /// Принимает двоичный режим <c>md5sum</c>, где вместо второго пробела стоит
    /// звёздочка. На Linux содержимое от режима не зависит, но вывод отличается,
    /// и отвергать его — значит ломать клиентов на ровном месте.
    /// </summary>
    [Fact]
    public void Parse_BinaryModeMarker_ParsedTheSameWay()
    {
        var result = ManifestFormat.Parse("d41d8cd98f00b204e9800998ecf8427e *bin/app", NoLimit);

        Assert.Empty(result.Errors);
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", result.Entries["bin/app"]);
    }

    /// <summary>
    /// Приводит сумму к нижнему регистру: часть утилит выдаёт её заглавными,
    /// а сравнение с манифестом сервера идёт по строке.
    /// </summary>
    [Fact]
    public void Parse_UpperCaseHash_NormalizedToLowerCase()
    {
        var result = ManifestFormat.Parse("D41D8CD98F00B204E9800998ECF8427E  bin/app", NoLimit);

        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", result.Entries["bin/app"]);
    }

    /// <summary>
    /// Сохраняет пробелы и кириллицу в пути. В формате <c>md5sum</c> путь —
    /// это всё от разделителя до конца строки, поэтому экранировать нечего.
    /// </summary>
    [Theory]
    [InlineData("docs/файл с пробелом.txt")]
    [InlineData("документы/отчёт.pdf")]
    [InlineData("dir/sub dir/a b c.bin")]
    public void Parse_SpacesAndCyrillicInPath_Preserved(string path)
    {
        var result = ManifestFormat.Parse($"d41d8cd98f00b204e9800998ecf8427e  {path}", NoLimit);

        Assert.Empty(result.Errors);
        Assert.True(result.Entries.ContainsKey(path));
    }

    /// <summary>
    /// Пропускает пустые строки и комментарии, не считая их ошибками:
    /// скрипт вправе добавить их в свой манифест.
    /// </summary>
    [Fact]
    public void Parse_BlankLinesAndComments_Ignored()
    {
        var content = "# заголовок\n\nd41d8cd98f00b204e9800998ecf8427e  a.txt\n\n";

        var result = ManifestFormat.Parse(content, NoLimit);

        Assert.Empty(result.Errors);
        Assert.Single(result.Entries);
    }

    /// <summary>
    /// Разбирает строки, разделённые как переводом строки, так и парой
    /// «возврат каретки и перевод строки»: манифест мог быть создан на Windows.
    /// </summary>
    [Fact]
    public void Parse_WindowsLineEndings_ParsedCorrectly()
    {
        var content = "d41d8cd98f00b204e9800998ecf8427e  a.txt\r\n5d41402abc4b2a76b9719d911017c592  b.txt\r\n";

        var result = ManifestFormat.Parse(content, NoLimit);

        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Entries.Count);
    }

    /// <summary>
    /// Отвергает строки, где первые 32 символа не являются шестнадцатеричной
    /// суммой, но продолжает разбор: клиенту полезнее получить весь список
    /// замечаний разом, чем останавливаться на первой плохой строке.
    /// </summary>
    [Fact]
    public void Parse_MalformedHash_ReportedButParsingContinues()
    {
        var content = "не-сумма-вообще-совсем-никак-нет  a.txt\nd41d8cd98f00b204e9800998ecf8427e  b.txt";

        var result = ManifestFormat.Parse(content, NoLimit);

        Assert.Single(result.Errors);
        Assert.Single(result.Entries);
        Assert.True(result.Entries.ContainsKey("b.txt"));
    }

    /// <summary>
    /// Отвергает экранированные пути. Утилита <c>md5sum</c> помечает их ведущим
    /// обратным слэшем, когда имя содержит перевод строки или сам слэш; принять
    /// такую строку — значит разъехаться с клиентом в разборе.
    /// </summary>
    [Fact]
    public void Parse_EscapedPath_Rejected()
    {
        var result = ManifestFormat.Parse(@"\d41d8cd98f00b204e9800998ecf8427e  a\nb.txt", NoLimit);

        Assert.Single(result.Errors);
        Assert.Empty(result.Entries);
    }

    /// <summary>
    /// Отвергает попытку выйти за пределы каталога и абсолютные пути.
    /// До обращения к файловой системе такой путь дойти не должен.
    /// </summary>
    [Theory]
    [InlineData("../secret.txt")]
    [InlineData("dir/../../etc/passwd")]
    [InlineData("/etc/passwd")]
    public void Parse_PathOutsideRoot_Rejected(string path)
    {
        var result = ManifestFormat.Parse($"d41d8cd98f00b204e9800998ecf8427e  {path}", NoLimit);

        Assert.NotEmpty(result.Errors);
        Assert.Empty(result.Entries);
    }

    /// <summary>
    /// При повторе пути берёт первое вхождение и отмечает остальные:
    /// молча затирать одно другим нельзя, результат зависел бы от порядка строк.
    /// </summary>
    [Fact]
    public void Parse_DuplicatePath_KeepsFirstAndReportsRest()
    {
        var content = "d41d8cd98f00b204e9800998ecf8427e  a.txt\n5d41402abc4b2a76b9719d911017c592  a.txt";

        var result = ManifestFormat.Parse(content, NoLimit);

        Assert.Single(result.Errors);
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", result.Entries["a.txt"]);
    }

    /// <summary>
    /// Прекращает разбор при достижении предела записей: тело запроса приходит
    /// извне, и его размер должен быть ограничен.
    /// </summary>
    [Fact]
    public void Parse_EntryLimitExceeded_ParsingStops()
    {
        var content = string.Join('\n', Enumerable.Range(0, 10)
            .Select(i => $"d41d8cd98f00b204e9800998ecf842{i:00}  file{i}.txt"));

        var result = ManifestFormat.Parse(content, maxEntries: 3);

        Assert.Equal(3, result.Entries.Count);
        Assert.Contains(result.Errors, e => e.Contains("длиннее", StringComparison.Ordinal));
    }

    /// <summary>Пустой манифест — не ошибка: так выглядит новый компьютер.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyManifest_ReturnsEmptyResultWithoutErrors(string? content)
    {
        var result = ManifestFormat.Parse(content, NoLimit);

        Assert.Empty(result.Entries);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// Записывает строку ровно в том виде, в каком её ждёт <c>md5sum -c</c>:
    /// два пробела между суммой и путём и перевод строки в конце.
    /// </summary>
    [Fact]
    public void AppendLine_WritesLineInMd5sumFormat()
    {
        var builder = new System.Text.StringBuilder();

        ManifestFormat.AppendLine(builder, "d41d8cd98f00b204e9800998ecf8427e", "bin/app");

        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e  bin/app\n", builder.ToString());
    }

    /// <summary>
    /// Проверяет обратимость: то, что записано, должно разбираться обратно
    /// без потерь. Иначе клиент не сможет свериться собственным манифестом.
    /// </summary>
    [Fact]
    public void AppendLineAndParse_AreMutuallyInverse()
    {
        var builder = new System.Text.StringBuilder();
        ManifestFormat.AppendLine(builder, "d41d8cd98f00b204e9800998ecf8427e", "каталог/файл с пробелом.bin");

        var result = ManifestFormat.Parse(builder.ToString(), NoLimit);

        Assert.Empty(result.Errors);
        Assert.Equal("d41d8cd98f00b204e9800998ecf8427e", result.Entries["каталог/файл с пробелом.bin"]);
    }
}
