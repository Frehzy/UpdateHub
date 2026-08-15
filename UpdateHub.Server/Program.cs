using UpdateHub.Server.Api.V1.Mappers;
using UpdateHub.Server.Infrastructure.Database;
using UpdateHub.Server.Infrastructure.Extensions;
using UpdateHub.Server.Infrastructure.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Настройка конфигурации
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// Регистрация сервисов
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddServices();
builder.Services.AddSecurity(builder.Configuration);
builder.Services.AddAutoMapper(x =>
{
    x.AddProfile<MappingProfile>();
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Настройка Kestrel для больших файлов и долгих соединений
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromHours(24);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
    options.Limits.MaxConcurrentConnections = 100;
    options.Limits.MaxConcurrentUpgradedConnections = 100;
});

// Настройка таймаутов для запросов
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromHours(24);
});

var app = builder.Build();

// Применение миграций при старте
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
}

// Настройка pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<JwtMiddleware>();
app.UseMiddleware<ClientAccessMiddleware>();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Запуск фоновых сервисов
app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("UpdateHub Server started successfully");
});

app.Run();