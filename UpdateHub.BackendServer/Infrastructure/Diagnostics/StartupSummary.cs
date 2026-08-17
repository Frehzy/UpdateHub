using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using UpdateHub.BackendServer.Application.Manifest;
using UpdateHub.BackendServer.Infrastructure.Configuration;

namespace UpdateHub.BackendServer.Infrastructure.Diagnostics;

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

        var state = app.Services.GetRequiredService<ManifestState>();
        var inContainer = IsRunningInContainer();

        var report = new StringBuilder();
        report.Append("\nСервер обновлений UpdateHub запущен\n");
        report.Append("  Окружение:        ").Append(app.Environment.EnvironmentName).Append('\n');

        AppendFilesFolder(report, config, state, inContainer);

        report.Append("  База данных:      ").Append(config.ResolvedDatabasePath).Append('\n');

        AppendAddresses(report, bound, inContainer);

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
    /// Добавляет в сводку раздел о каталоге, файлы из которого раздаются клиентам.
    /// </summary>
    /// <param name="report">Приёмник текста.</param>
    /// <param name="config">Настройки раздачи.</param>
    /// <param name="state">Состояние манифеста.</param>
    /// <param name="inContainer">Признак запуска в контейнере.</param>
    private static void AppendFilesFolder(
        StringBuilder report,
        UpdateHubConfig config,
        ManifestState state,
        bool inContainer)
    {
        var fullPath = config.ResolvedFilesPath;

        report.Append("  Каталог раздачи:  ").Append(fullPath).Append('\n');

        if (inContainer)
        {
            // Внутри контейнера это точка монтирования. Какая папка Windows
            // за ней стоит, знает только параметр -v команды docker run.
            report.Append("                    (точка монтирования в контейнере; папка Windows задаётся параметром -v)\n");
        }

        if (!Directory.Exists(fullPath))
        {
            report.Append("                    ВНИМАНИЕ: каталог отсутствует\n");
        }

        report.Append("  Опрос каталога:   каждые ").Append(config.PollIntervalSeconds).Append(" с\n");

        report.Append("  Состояние:        ");
        if (state.LastScanCompletedAt is null)
        {
            report.Append("первый обход выполняется\n");
        }
        else
        {
            report.Append("файлов ").Append(state.EntryCount)
                  .Append(", объём ").Append(FormatSize(state.TotalSizeBytes)).Append('\n');

            if (state.RejectedPaths.Count > 0)
            {
                report.Append("                    отвергнуто файлов: ").Append(state.RejectedPaths.Count)
                      .Append(" (подробности в /api/v1/admin/manifest/status)\n");
            }
        }
    }

    /// <summary>
    /// Переводит размер в байтах в удобочитаемый вид.
    /// </summary>
    /// <param name="bytes">Размер в байтах.</param>
    /// <returns>Строка вида «6,8 ГБ».</returns>
    private static string FormatSize(long bytes)
    {
        string[] units = ["Б", "КБ", "МБ", "ГБ", "ТБ"];
        double size = bytes;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} Б" : $"{size:0.#} {units[unit]}";
    }

    /// <summary>
    /// Определяет, выполняется ли приложение внутри контейнера.
    /// </summary>
    /// <returns><see langword="true"/>, если это контейнер.</returns>
    /// <remarks>Переменную выставляют официальные образы .NET.</remarks>
    private static bool IsRunningInContainer()
        => string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Добавляет в сводку раздел с адресами, по которым сервер доступен клиентам.
    /// </summary>
    /// <param name="report">Приёмник текста.</param>
    /// <param name="bound">Адреса, на которых слушает Kestrel.</param>
    /// <param name="inContainer">Признак запуска в контейнере.</param>
    private static void AppendAddresses(StringBuilder report, ICollection<string> bound, bool inContainer)
    {
        if (bound.Count == 0)
        {
            report.Append("  Адреса:           не назначены\n");
            return;
        }

        report.Append("  Слушает:          ").Append(string.Join(", ", bound)).Append('\n');

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
