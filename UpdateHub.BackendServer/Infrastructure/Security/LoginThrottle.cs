using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using UpdateHub.BackendServer.Infrastructure.Configuration;

namespace UpdateHub.BackendServer.Infrastructure.Security;

/// <summary>
/// Замедляет подбор пароля: после нескольких неудач вход под этой учётной
/// записью на время закрывается.
/// </summary>
/// <param name="config">Настройки.</param>
/// <remarks>
/// Периметр закрыт межсетевым экраном и криптомаршрутизатором, но не от того,
/// кто уже в сети: перебор пароля изнутри ничем не мешало бы вести часами.
/// <para>
/// Считаются <b>только неудачные</b> попытки, и <b>по имени учётной записи</b>.
/// Оба решения приняты после того, как прежний ограничитель пришлось снять:
/// он считал обращения по адресу и любые, включая удачные. За
/// криптомаршрутизатором весь парк машин приходит с одного адреса, поэтому
/// одиннадцатая машина за минуту получала отказ и переставала обновляться —
/// молча, в контуре, куда нужно ехать. А расходовала запас обычная работа,
/// потому что удачные входы тоже шли в счёт.
/// </para>
/// <para>
/// Здесь ни то, ни другое невозможно. Удачный вход счётчик обнуляет, поэтому
/// работающая машина не задевается никогда, сколько бы их ни было за одним
/// адресом. Счёт по учётной записи означает, что чужие попытки не мешают
/// её владельцу — они мешают только подбору её же пароля.
/// </para>
/// <para>
/// Состояние живёт в памяти. Перезапуск сервера сбрасывает счётчики, и это
/// приемлемо: перебор, растянутый через перезапуски сервера, — не та угроза,
/// от которой здесь защищаются.
/// </para>
/// </remarks>
public sealed class LoginThrottle(IOptions<UpdateHubConfig> config)
{
    private readonly UpdateHubConfig _config = config.Value;
    private readonly ConcurrentDictionary<string, Attempts> _byUsername = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Сообщает, закрыт ли вход под этой учётной записью прямо сейчас.
    /// </summary>
    /// <param name="username">Имя учётной записи.</param>
    /// <returns>
    /// Время, которое осталось ждать, либо <see langword="null"/>,
    /// если вход открыт.
    /// </returns>
    public TimeSpan? GetRemainingBlock(string username)
    {
        if (_config.LoginFailureLimit <= 0 || string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        if (!_byUsername.TryGetValue(username, out var attempts))
        {
            return null;
        }

        var blockedUntil = attempts.BlockedUntil;
        if (blockedUntil is null || blockedUntil <= DateTime.UtcNow)
        {
            return null;
        }

        return blockedUntil.Value - DateTime.UtcNow;
    }

    /// <summary>
    /// Отмечает неудачную попытку входа.
    /// </summary>
    /// <param name="username">Имя учётной записи, под которым пытались войти.</param>
    /// <remarks>
    /// Записывается и для несуществующего логина: иначе перебор имён обходил бы
    /// ограничение целиком.
    /// </remarks>
    public void RegisterFailure(string username)
    {
        if (_config.LoginFailureLimit <= 0 || string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        var attempts = _byUsername.GetOrAdd(username, _ => new Attempts());
        attempts.RegisterFailure(_config.LoginFailureLimit, TimeSpan.FromMinutes(_config.LoginBlockMinutes));
    }

    /// <summary>
    /// Отмечает удачный вход и снимает накопленные неудачи.
    /// </summary>
    /// <param name="username">Имя учётной записи.</param>
    /// <remarks>
    /// Ключевое место: обычная работа не расходует запас. Машина, которая
    /// входит по расписанию с верным паролем, ограничителя не встретит.
    /// </remarks>
    public void RegisterSuccess(string username)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            _byUsername.TryRemove(username, out _);
        }
    }

    /// <summary>
    /// Неудачные попытки по одной учётной записи.
    /// </summary>
    /// <remarks>
    /// Отдельный тип, потому что счётчик и момент разблокировки обязаны меняться
    /// вместе: иначе две одновременные попытки увидели бы половину изменения.
    /// </remarks>
    private sealed class Attempts
    {
        private readonly object _lock = new();
        private int _failures;

        /// <summary>Момент, до которого вход закрыт; пусто — открыт.</summary>
        public DateTime? BlockedUntil { get; private set; }

        /// <summary>
        /// Учитывает неудачу и при достижении предела закрывает вход.
        /// </summary>
        /// <param name="limit">Сколько неудач допускается.</param>
        /// <param name="block">На сколько закрывать вход.</param>
        public void RegisterFailure(int limit, TimeSpan block)
        {
            lock (_lock)
            {
                // Истёкшая блокировка начинает отсчёт заново: иначе одна давняя
                // серия неудач закрывала бы вход навсегда, по одной попытке.
                if (BlockedUntil is not null && BlockedUntil <= DateTime.UtcNow)
                {
                    _failures = 0;
                    BlockedUntil = null;
                }

                _failures++;

                if (_failures >= limit)
                {
                    BlockedUntil = DateTime.UtcNow.Add(block);
                }
            }
        }
    }
}
