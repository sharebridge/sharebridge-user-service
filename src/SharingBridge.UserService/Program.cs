using SharingBridge.UserService;

var builder = WebApplication.CreateBuilder(args);

// Render / dotenv-style: prefer environment variables over appsettings.
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<GoogleAuthService>();

var dataAccess = DataAccessOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(dataAccess);

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration["DATABASE_URL"];
var useMemory = string.Equals(
    builder.Configuration["USER_STORE"] ?? Environment.GetEnvironmentVariable("USER_STORE"),
    "memory",
    StringComparison.OrdinalIgnoreCase);

if (useMemory || string.IsNullOrWhiteSpace(databaseUrl))
{
    if (!useMemory && builder.Environment.IsProduction())
    {
        throw new InvalidOperationException("DATABASE_URL is required. See configuration/database.md.");
    }

    builder.Services.AddSingleton<IUserStore, InMemoryUserStore>();
}
else
{
    try
    {
        using var startupLogFactory = LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
        var store = await PostgresUserStore.CreateAsync(
            databaseUrl,
            dataAccess,
            startupLogFactory.CreateLogger("Startup"));
        builder.Services.AddSingleton<IUserStore>(store);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            "Failed to open DATABASE_URL with Npgsql. Prefer Supabase Session pooler " +
            "(port 5432 on *.pooler.supabase.com), not Transaction (6543). " +
            "Use the Postgres URI, not the anon/service_role API key. " +
            "Tune DB_POOL_* / DB_RETRY_* via environment variables.",
            ex);
    }
}

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
    options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never;
});

var app = builder.Build();

var corsOrigins = CorsSupport.ParseOrigins(app.Configuration["WEB_CORS_ORIGINS"]);

app.Use(async (ctx, next) =>
{
    // initiator-presets → donor-presets (path alias)
    var path = ctx.Request.Path.Value ?? "";
    if (path.Contains("/initiator-presets", StringComparison.Ordinal))
    {
        ctx.Request.Path = path.Replace("/initiator-presets", "/donor-presets", StringComparison.Ordinal);
    }

    CorsSupport.Apply(ctx, corsOrigins);
    if (HttpMethods.IsOptions(ctx.Request.Method))
    {
        ctx.Response.StatusCode = CorsSupport.ResolveAllowOrigin(ctx.Request.Headers.Origin.ToString(), corsOrigins) is not null
            ? StatusCodes.Status204NoContent
            : StatusCodes.Status403Forbidden;
        return;
    }

    await next();
});

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapPresetEndpoints();
app.MapFallback(() =>
    Results.Json(new ErrorBody { Code = "not_found", Message = "Route not found." }, statusCode: 404));

// Render sets PORT for Docker web services; local default 8081.
var port = Environment.GetEnvironmentVariable("PORT")
    ?? app.Configuration["PORT"]
    ?? "8081";
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{port}");

var listenLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
listenLogger.LogInformation("Binding http://0.0.0.0:{Port}", port);

LogStartup(app);
app.Run();

static void LogStartup(WebApplication app)
{
    var config = app.Configuration;
    var issues = new List<string>();
    if (string.IsNullOrWhiteSpace(config["DATABASE_URL"]) &&
        !string.Equals(config["USER_STORE"], "memory", StringComparison.OrdinalIgnoreCase))
    {
        issues.Add("DATABASE_URL is unset");
    }

    if (string.IsNullOrWhiteSpace(config["WEB_CORS_ORIGINS"]))
    {
        issues.Add("WEB_CORS_ORIGINS is unset");
    }

    if (string.IsNullOrWhiteSpace(config["GOOGLE_CLIENT_ID_WEB"]))
    {
        issues.Add("GOOGLE_CLIENT_ID_WEB is unset");
    }

    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogInformation("User service listening (ASP.NET Core / C#)");
    foreach (var issue in issues)
    {
        logger.LogWarning("Startup: {Issue}", issue);
    }
}

public partial class Program;
