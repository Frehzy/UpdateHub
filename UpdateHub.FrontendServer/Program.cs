using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UpdateHub.FrontendServer;
using UpdateHub.FrontendServer.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Адрес сервера берётся из адреса самой страницы: панель отдаёт тот же сервер,
// к которому она обращается. Отдельной настройки не требуется, и при переносе
// в закрытый контур менять нечего.
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),

    // Обычные запросы панели короткие. Долгих среди них один — внеочередной
    // обход каталога с шестигигабайтным образом, поэтому запас взят с избытком.
    Timeout = TimeSpan.FromMinutes(10)
});

builder.Services.AddScoped<AuthState>();
builder.Services.AddScoped<ApiClient>();

await builder.Build().RunAsync();
