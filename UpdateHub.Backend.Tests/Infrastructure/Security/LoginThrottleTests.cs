using Microsoft.Extensions.Options;
using UpdateHub.BackendServer.Infrastructure.Configuration;
using UpdateHub.BackendServer.Infrastructure.Security;

namespace UpdateHub.Backend.Tests.Infrastructure.Security;

/// <summary>
/// Проверяет ограничитель подбора пароля.
/// </summary>
/// <remarks>
/// Замена ограничителя, который пришлось снять. Тот считал обращения по адресу
/// и любые, включая удачные: за криптомаршрутизатором весь парк машин приходит
/// с одного адреса, поэтому одиннадцатая машина за минуту переставала получать
/// обновления — молча, в контуре, куда нужно ехать.
/// <para>
/// Здесь проверяется прежде всего то, чего не хватало тому варианту: удачный
/// вход обнуляет счёт, а счёт ведётся по учётной записи. Из этих двух свойств
/// и следует, что работающие машины ограничителя не встретят никогда.
/// </para>
/// </remarks>
public class LoginThrottleTests
{
    /// <summary>Собирает ограничитель с заданными настройками.</summary>
    /// <param name="failureLimit">Сколько неудач допускается.</param>
    /// <param name="blockMinutes">На сколько минут закрывать вход.</param>
    /// <returns>Ограничитель.</returns>
    private static LoginThrottle Create(int failureLimit = 3, int blockMinutes = 5)
        => new(Options.Create(new UpdateHubConfig
        {
            LoginFailureLimit = failureLimit,
            LoginBlockMinutes = blockMinutes
        }));

    /// <summary>Пока неудач нет, вход открыт.</summary>
    [Fact]
    public void GetRemainingBlock_WithoutFailures_ReturnsNull()
    {
        var throttle = Create();

        Assert.Null(throttle.GetRemainingBlock("ivanov"));
    }

    /// <summary>Неудач меньше предела — вход ещё открыт.</summary>
    /// <remarks>
    /// Половина проверки, без которой следующая ничего не значит: ограничитель,
    /// закрывающий вход с первой же ошибки, прошёл бы её.
    /// </remarks>
    [Fact]
    public void GetRemainingBlock_BelowLimit_StillOpen()
    {
        var throttle = Create(failureLimit: 3);

        throttle.RegisterFailure("ivanov");
        throttle.RegisterFailure("ivanov");

        Assert.Null(throttle.GetRemainingBlock("ivanov"));
    }

    /// <summary>При достижении предела вход закрывается.</summary>
    [Fact]
    public void GetRemainingBlock_AtLimit_Blocks()
    {
        var throttle = Create(failureLimit: 3, blockMinutes: 5);

        throttle.RegisterFailure("ivanov");
        throttle.RegisterFailure("ivanov");
        throttle.RegisterFailure("ivanov");

        var remaining = throttle.GetRemainingBlock("ivanov");

        Assert.NotNull(remaining);
        Assert.True(remaining.Value > TimeSpan.Zero);
        Assert.True(remaining.Value <= TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Удачный вход снимает накопленные неудачи.
    /// </summary>
    /// <remarks>
    /// Главное свойство. Прежний ограничитель расходовал запас удачными входами,
    /// то есть исчерпывала его обычная работа, а не перебор. Здесь машина,
    /// входящая по расписанию с верным паролем, счётчик каждый раз обнуляет.
    /// </remarks>
    [Fact]
    public void RegisterSuccess_ResetsFailures()
    {
        var throttle = Create(failureLimit: 3);

        throttle.RegisterFailure("ivanov");
        throttle.RegisterFailure("ivanov");
        throttle.RegisterSuccess("ivanov");

        // Двух неудач после обнуления недостаточно: предел считается заново.
        throttle.RegisterFailure("ivanov");
        throttle.RegisterFailure("ivanov");

        Assert.Null(throttle.GetRemainingBlock("ivanov"));
    }

    /// <summary>
    /// Счёт ведётся по учётной записи: чужие неудачи владельцу не мешают.
    /// </summary>
    /// <remarks>
    /// Второе главное свойство. Именно из него следует, что подбор пароля
    /// администратора не закрывает вход машинам, работающим под своими записями,
    /// — а прежний вариант по адресу закрывал их все разом.
    /// </remarks>
    [Fact]
    public void RegisterFailure_CountedPerUsername()
    {
        var throttle = Create(failureLimit: 2);

        throttle.RegisterFailure("admin");
        throttle.RegisterFailure("admin");

        Assert.NotNull(throttle.GetRemainingBlock("admin"));
        Assert.Null(throttle.GetRemainingBlock("ivanov"));
    }

    /// <summary>Регистр в имени учётной записи не позволяет обойти счёт.</summary>
    /// <remarks>
    /// Логины сравниваются без учёта регистра, поэтому «Admin» и «admin» —
    /// одна и та же запись. Иначе предел обходился бы сменой регистра.
    /// </remarks>
    [Fact]
    public void RegisterFailure_IgnoresUsernameCase()
    {
        var throttle = Create(failureLimit: 2);

        throttle.RegisterFailure("admin");
        throttle.RegisterFailure("ADMIN");

        Assert.NotNull(throttle.GetRemainingBlock("Admin"));
    }

    /// <summary>Нулевой предел отключает ограничитель.</summary>
    /// <remarks>
    /// Ноль как «отключено» — то же соглашение, что у остальных настроек:
    /// BackupIntervalHours, BackupKeepCount, сроков хранения.
    /// </remarks>
    [Fact]
    public void GetRemainingBlock_ZeroLimit_NeverBlocks()
    {
        var throttle = Create(failureLimit: 0);

        for (var attempt = 0; attempt < 50; attempt++)
        {
            throttle.RegisterFailure("ivanov");
        }

        Assert.Null(throttle.GetRemainingBlock("ivanov"));
    }

    /// <summary>Пустое имя учётной записи ограничитель не учитывает.</summary>
    /// <remarks>
    /// Форма входа без логина отклоняется раньше, на разборе запроса. Проверка
    /// нужна затем, чтобы пустая строка не стала общей ячейкой счёта, в которой
    /// смешались бы попытки всех записей.
    /// </remarks>
    [Fact]
    public void RegisterFailure_EmptyUsername_Ignored()
    {
        var throttle = Create(failureLimit: 1);

        throttle.RegisterFailure("");
        throttle.RegisterFailure("   ");

        Assert.Null(throttle.GetRemainingBlock(""));
        Assert.Null(throttle.GetRemainingBlock("ivanov"));
    }

    /// <summary>
    /// Истёкшая блокировка начинает отсчёт заново, а не закрывает вход
    /// с первой же следующей неудачи.
    /// </summary>
    /// <remarks>
    /// Иначе одна давняя серия неудач превращала бы учётную запись в закрытую
    /// навсегда: каждая попытка после снятия блокировки снова упиралась бы
    /// в остаток прежнего счёта.
    /// </remarks>
    [Fact]
    public void RegisterFailure_AfterBlockExpired_StartsOver()
    {
        // Нулевая длительность блокировки истекает сразу же.
        var throttle = Create(failureLimit: 2, blockMinutes: 0);

        throttle.RegisterFailure("ivanov");
        throttle.RegisterFailure("ivanov");

        Assert.Null(throttle.GetRemainingBlock("ivanov"));

        // Одной неудачи снова недостаточно: счёт пошёл с начала.
        throttle.RegisterFailure("ivanov");

        Assert.Null(throttle.GetRemainingBlock("ivanov"));
    }
}
