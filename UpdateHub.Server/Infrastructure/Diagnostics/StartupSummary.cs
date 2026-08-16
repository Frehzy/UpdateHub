using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UpdateHub.Server.Infrastructure.Configuration;

namespace UpdateHub.Server.Infrastructure.Diagnostics;

/// <summary>
/// Выводит в журнал сводку о запущенном сервере: по каким адресам он доступен
/// и с какими основными настройками работает.
/// </summary>
public static class StartupSummary
{
    /// <summary>
    /// Печатает сводку. Вызывается после старта, когда адреса уже назначены.
    /// </summary>
    /// <param name="app">Запущенное приложение.</param>
    public static void Log(WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("UpdateHub.Startup");
        var config = app.Services.GetRequiredService<IOptions<UpdateHubConfig>>().Value;

        var bound = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses
            ?? [];

        var report = new StringBuilder();
        report.Append("\nСервер обновлений UpdateHub запущен\n");
        report.Append("  Окружение:        ").Append(app.Environment.EnvironmentName).Append('\n');
        report.Append("  Каталог раздачи:  ").Append(Path.GetFullPath(config.FilesPath)).Append('\n');
        report.Append("  База данных:      ").Append(Path.GetFullPath(config.DatabasePath)).Append('\n');
        report.Append("  Опрос каталога:   каждые ").Append(config.PollIntervalSeconds).Append(" с\n");

        AppendAddresses(report, bound);

        if (app.Environment.IsDevelopment())
        {
            var first = bound.FirstOrDefault();
            if (first is not null)
            {
                report.Append("  Swagger:          ").Append(NormalizeForBrowser(first)).Append("/swagger\n");
            }
        }

        logger.LogInformation("{Summary}", report.ToString());
    }

    /// <summary>
    /// Добавляет в сводку раздел с адресами, по которым сервер доступен клиентам.
    /// </summary>
    /// <param name="report">Приёмник текста.</param>
    /// <param name="bound">Адреса, на которых слушает Kestrel.</param>
    private static void AppendAddresses(StringBuilder report, ICollection<string> bound)
    {
        if (bound.Count == 0)
        {
            report.Append("  Адреса:           не назначены\n");
            return;
        }

        report.Append("  Слушает:          ").Append(string.Join(", ", bound)).Append('\n');

        var inContainer = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (bound.All(IsLoopbackOnly))
        {
            report.Append(
                "  ВНИМАНИЕ:         сервер слушает только локальную петлю, с других машин он недоступен.\n" +
                "                    Для доступа по сети запустите профиль 'http-lan' либо задайте\n" +
                "                    ASPNETCORE_URLS=http://+:5083\n");
            return;
        }

        // Адреса самой машины имеет смысл показывать только когда Kestrel слушает
        // все интерфейсы: при явном адресе он и так напечатан выше.
        if (!bound.Any(IsWildcard))
        {
            return;
        }

        var port = ExtractPort(bound.First(IsWildcard));

        if (inContainer)
        {
            // Внутри контейнера видны только его собственные адреса, а клиенты
            // обращаются по адресу хоста Docker. Печатать их как рабочие — обман.
            report.Append(
                "  Запущен в контейнере: клиенты обращаются по адресу хоста Docker и порту,\n" +
                "                    указанному в параметре -p, а не по внутреннему адресу контейнера\n");
            return;
        }

        var addresses = GetLocalIPv4Addresses();
        if (addresses.Count == 0)
        {
            return;
        }

        report.Append("  Доступен по:\n");
        foreach (var (address, adapter) in addresses)
        {
            report.Append("      http://").Append(address).Append(':').Append(port)
                  .Append("   (").Append(adapter).Append(")\n");
        }
    }

    /// <summary>
    /// Возвращает адреса IPv4 работающих сетевых адаптеров, кроме локальной петли.
    /// </summary>
    /// <returns>Пары «адрес — название адаптера».</returns>
    private static List<(string Address, string Adapter)> GetLocalIPv4Addresses()
    {
        var result = new List<(string, string)>();

        try
        {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up ||
                    adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                foreach (var unicast in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(unicast.Address))
                    {
                        result.Add((unicast.Address.ToString(), adapter.Name));
                    }
                }
            }
        }
        catch (NetworkInformationException)
        {
            // Перечисление адаптеров — украшение журнала, а не работа сервера:
            // отказ в правах или урезанное сетевое окружение не должны мешать старту.
        }

        return result;
    }

    /// <summary>Определяет, слушает ли адрес все интерфейсы.</summary>
    /// <param name="address">Адрес привязки Kestrel.</param>
    /// <returns><see langword="true"/>, если адрес означает «все интерфейсы».</returns>
    private static bool IsWildcard(string address)
        => address.Contains("://+", StringComparison.Ordinal)
        || address.Contains("://0.0.0.0", StringComparison.Ordinal)
        || address.Contains("://[::]", StringComparison.Ordinal);

    /// <summary>Определяет, ограничен ли адрес локальной петлёй.</summary>
    /// <param name="address">Адрес привязки Kestrel.</param>
    /// <returns><see langword="true"/>, если адрес доступен только с этой машины.</returns>
    private static bool IsLoopbackOnly(string address)
        => address.Contains("://localhost", StringComparison.OrdinalIgnoreCase)
        || address.Contains("://127.0.0.1", StringComparison.Ordinal)
        || address.Contains("://[::1]", StringComparison.Ordinal);

    /// <summary>Извлекает номер порта из адреса привязки.</summary>
    /// <param name="address">Адрес привязки Kestrel.</param>
    /// <returns>Порт либо пустая строка, если разобрать не удалось.</returns>
    private static string ExtractPort(string address)
    {
        var lastColon = address.LastIndexOf(':');
        if (lastColon < 0 || lastColon == address.Length - 1)
        {
            return string.Empty;
        }

        var port = address[(lastColon + 1)..].TrimEnd('/');
        return port.All(char.IsDigit) ? port : string.Empty;
    }

    /// <summary>
    /// Приводит адрес привязки к виду, пригодному для открытия в браузере.
    /// </summary>
    /// <param name="address">Адрес привязки Kestrel.</param>
    /// <returns>Адрес, по которому можно перейти с этой же машины.</returns>
    private static string NormalizeForBrowser(string address)
        => address
            .Replace("://+", "://localhost", StringComparison.Ordinal)
            .Replace("://0.0.0.0", "://localhost", StringComparison.Ordinal)
            .Replace("://[::]", "://localhost", StringComparison.Ordinal)
            .TrimEnd('/');
}
