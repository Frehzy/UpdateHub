using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi;
using System.IO.Compression;
using System.Reflection;
using UpdateHub.Server.Api.V1.Mappers;
using UpdateHub.Server.Infrastructure.Database;
using UpdateHub.Server.Infrastructure.Diagnostics;
using UpdateHub.Server.Infrastructure.Extensions;
using UpdateHub.Server.Infrastructure.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Источники конфигурации настраивает CreateBuilder: appsettings.json,
// appsettings.{Environment}.json, пользовательские секреты, переменные окружения
// и аргументы командной строки — именно в этом порядке. Добавлять их повторно
// нельзя: базовый appsettings.json окажется последним и перекроет всё остальное.

builder.Services.AddAppConfiguration(builder.Configuration);
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddRepositories();
builder.Services.AddApplicationServices();
builder.Services.AddSecurity(builder.Configuration);

builder.Services.AddAutoMapper(options => options.AddProfile<MappingProfile>());
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

// Манифест и планы синхронизации — текст, который сжимается примерно в восемь раз.
// На канале 2 Мбит/с это заметно; клиенту достаточно флага curl --compressed.
builder.Services.AddResponseCompression(options =>
{
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["text/plain"]);
});
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

// Форма используется только для входа и заявок — там несколько коротких полей.
// Манифест приходит отдельным запросом как text/plain, поэтому ограничение
// на число полей формы ему не мешает.
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = 64;
    options.ValueLengthLimit = 8 * 1024;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "UpdateHub",
        Version = "v1",
        Description =
            "Сервер обновлений. Клиентская часть (/api/v1/auth, /sync, /files, /enroll) отвечает " +
            "текстом в формате md5sum и «ключ=значение» — для bash-скрипта без jq. " +
            "Панель управления (/api/v1/admin) работает с JSON."
    });

    // XML-комментарии из кода становятся описаниями в Swagger.
    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Access-токен, полученный от /api/v1/auth/login"
    });
});

builder.WebHost.ConfigureKestrel(options =>
{
    // Отдача шестигигабайтного образа по каналу 2 Мбит/с занимает около семи часов,
    // поэтому ограничение на минимальную скорость снято, а время удержания
    // соединения увеличено. Заголовки при этом по-прежнему обязаны прийти быстро.
    options.Limits.MinResponseDataRate = null;
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(1);

    // Тело запроса — это манифест клиента, десятки килобайт. Загрузок на сервер нет.
    options.Limits.MaxRequestBodySize = 16 * 1024 * 1024;
});

var app = builder.Build();

// Подготовка базы: каталог, схема, режим WAL и первый администратор.
using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await initializer.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = "UpdateHub API");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseResponseCompression();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

// Сводка печатается после старта: до этого момента Kestrel ещё не назначил адреса.
app.Lifetime.ApplicationStarted.Register(() => StartupSummary.Log(app));

app.Run();
