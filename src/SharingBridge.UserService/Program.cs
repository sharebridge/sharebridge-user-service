using System.Text.Json;
using SharingBridge.UserService;

var builder = WebApplication.CreateBuilder(args);

// Render / dotenv-style: prefer environment variables over appsettings.
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<GoogleAuthService>();

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
            startupLogFactory.CreateLogger("Startup"));
        builder.Services.AddSingleton<IUserStore>(store);
    }
    catch (Exception ex)
    {
        throw new InvalidOperationException(
            "Failed to open DATABASE_URL with Npgsql. Prefer Supabase Session pooler " +
            "(port 5432 on *.pooler.supabase.com), not Transaction (6543). " +
            "Use the Postgres URI, not the anon/service_role API key.",
            ex);
    }
}

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
    options.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never;
});

var app = builder.Build();

var corsOrigins = ParseCorsOrigins(app.Configuration["WEB_CORS_ORIGINS"]);

app.Use(async (ctx, next) =>
{
    // initiator-presets → donor-presets (Node path alias)
    var path = ctx.Request.Path.Value ?? "";
    if (path.Contains("/initiator-presets", StringComparison.Ordinal))
    {
        ctx.Request.Path = path.Replace("/initiator-presets", "/donor-presets", StringComparison.Ordinal);
    }

    ApplyCors(ctx, corsOrigins);
    if (HttpMethods.IsOptions(ctx.Request.Method))
    {
        ctx.Response.StatusCode = ResolveCorsAllowOrigin(ctx.Request.Headers.Origin.ToString(), corsOrigins) is not null
            ? StatusCodes.Status204NoContent
            : StatusCodes.Status403Forbidden;
        return;
    }

    await next();
});

app.MapGet("/health", (IConfiguration config) =>
{
    var logLevel = (config["LOG_LEVEL"] ?? "warn").Trim().ToLowerInvariant();
    return Results.Json(new
    {
        ok = true,
        service = "user-service",
        config = new
        {
            service = "user-service",
            database_url_set = !string.IsNullOrWhiteSpace(config["DATABASE_URL"]),
            web_cors_origins_set = !string.IsNullOrWhiteSpace(config["WEB_CORS_ORIGINS"]),
            google_client_id_web_set = !string.IsNullOrWhiteSpace(config["GOOGLE_CLIENT_ID_WEB"]),
            google_client_id_android_set = !string.IsNullOrWhiteSpace(config["GOOGLE_CLIENT_ID_ANDROID"]),
            auth_token_secret_set = !string.IsNullOrWhiteSpace(config["AUTH_TOKEN_SECRET"]),
            log_level = logLevel
        }
    });
});

app.MapPost("/v1/auth/google", async (
    GoogleAuthRequest body,
    IUserStore store,
    GoogleAuthService googleAuth,
    TokenService tokens,
    ILoggerFactory loggerFactory,
    CancellationToken ct) =>
{
    var logger = loggerFactory.CreateLogger("Auth");
    try
    {
        var idToken = body.IdToken?.Trim() ?? "";
        var accessToken = body.AccessToken?.Trim() ?? "";
        if (idToken.Length == 0 && accessToken.Length == 0)
        {
            return Results.Json(
                new ErrorBody { Code = "invalid_request", Message = "id_token or access_token is required." },
                statusCode: 400);
        }

        var clientType = string.IsNullOrWhiteSpace(body.ClientType)
            ? "web"
            : body.ClientType.Trim().ToLowerInvariant();

        // Prefer access_token when both are present (Node behaviour).
        var profile = accessToken.Length > 0
            ? await googleAuth.VerifyAccessTokenAsync(accessToken, ct)
            : await googleAuth.VerifyIdTokenAsync(idToken, ct);

        if (string.IsNullOrWhiteSpace(profile.Email))
        {
            return Results.Json(
                new ErrorBody
                {
                    Code = "invalid_request",
                    Message = "Google account must expose an email address."
                },
                statusCode: 400);
        }

        var user = await DbRetry.ExecuteAsync(
            token => store.FindOrCreateGoogleUserAsync(
                profile.GoogleSub, profile.Email, profile.Name, profile.Picture, token),
            logger,
            maxAttempts: 3,
            ct);
        await DbRetry.ExecuteAsync(
            token => store.EnsureRoleAsync(user.Id, Roles.Donor, token),
            logger,
            maxAttempts: 3,
            ct);
        var roles = await DbRetry.ExecuteAsync(
            token => store.GetRolesForUserAsync(user.Id, token),
            logger,
            maxAttempts: 3,
            ct);
        var roleError = Roles.ClientRoleError(clientType, roles);
        if (roleError is not null)
        {
            return Results.Json(roleError, statusCode: 403);
        }

        var role = Roles.RoleForClientType(clientType, roles);
        var token = tokens.Mint(user.Id, role, roles);
        user.Role = role;
        return Results.Json(new AuthResponse { Token = token, TokenType = "Bearer", User = user });
    }
    catch (Exception ex) when (ex.Message.Contains("invalid_request", StringComparison.Ordinal))
    {
        return Results.Json(
            new ErrorBody { Code = "invalid_request", Message = ex.Message },
            statusCode: 400);
    }
    catch (Exception ex) when (IsDatabaseFailure(ex))
    {
        logger.LogError(ex, "Database error during Google sign-in");
        return Results.Json(
            new ErrorBody
            {
                Code = "database_unavailable",
                Message = "Could not reach the user database. Try again shortly."
            },
            statusCode: 503);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Google sign-in failed");
        return Results.Json(
            new ErrorBody
            {
                Code = "invalid_google_token",
                Message = string.IsNullOrWhiteSpace(ex.Message) ? "Google sign-in failed." : ex.Message
            },
            statusCode: 401);
    }
});

async Task<IResult> GetPresets(
    string userId,
    HttpRequest request,
    IUserStore store,
    TokenService tokens,
    CancellationToken ct)
{
    var auth = RequireUser(request, tokens, userId);
    if (auth.Error is not null)
    {
        return auth.Error;
    }

    var presets = await store.ListDonorPresetsAsync(auth.UserId!, ct);
    return Results.Json(new { presets });
}

async Task<IResult> PutPresets(
    string userId,
    PresetsBody body,
    HttpRequest request,
    IUserStore store,
    TokenService tokens,
    CancellationToken ct)
{
    var auth = RequireUser(request, tokens, userId);
    if (auth.Error is not null)
    {
        return auth.Error;
    }

    if (body.Presets is null)
    {
        return Results.Json(
            new ErrorBody { Code = "invalid_request", Message = "presets must be an array." },
            statusCode: 400);
    }

    for (var i = 0; i < body.Presets.Count; i++)
    {
        var validationError = DonorPresetUtils.ValidatePreset(body.Presets[i], i);
        if (validationError is not null)
        {
            return Results.Json(
                new ErrorBody { Code = "invalid_request", Message = validationError },
                statusCode: 400);
        }
    }

    await store.GetOrCreateUserAsync(auth.UserId!, null, null, ct);
    var presets = await store.ReplaceDonorPresetsAsync(auth.UserId!, body.Presets, ct);
    return Results.Json(new { presets });
}

async Task<IResult> DeletePresetItem(
    string userId,
    DeletePresetBody body,
    HttpRequest request,
    IUserStore store,
    TokenService tokens,
    CancellationToken ct)
{
    var auth = RequireUser(request, tokens, userId);
    if (auth.Error is not null)
    {
        return auth.Error;
    }

    if (string.IsNullOrWhiteSpace(body.RestaurantName))
    {
        return Results.Json(
            new ErrorBody { Code = "invalid_request", Message = "restaurant_name is required." },
            statusCode: 400);
    }

    if (string.IsNullOrWhiteSpace(body.OrderUrl))
    {
        return Results.Json(
            new ErrorBody { Code = "invalid_request", Message = "order_url is required." },
            statusCode: 400);
    }

    await store.GetOrCreateUserAsync(auth.UserId!, null, null, ct);
    var presets = await store.DeleteDonorPresetAsync(
        auth.UserId!, body.RestaurantName.Trim(), body.OrderUrl.Trim(), ct);
    return Results.Json(new { presets });
}

app.MapGet("/v1/users/{userId}/donor-presets", GetPresets);
app.MapGet("/v1/users/{userId}/initiator-presets", GetPresets);
app.MapPut("/v1/users/{userId}/donor-presets", PutPresets);
app.MapPut("/v1/users/{userId}/initiator-presets", PutPresets);
app.MapPost("/v1/users/{userId}/donor-presets/delete-item", DeletePresetItem);
app.MapPost("/v1/users/{userId}/initiator-presets/delete-item", DeletePresetItem);
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

static bool IsDatabaseFailure(Exception ex)
{
    for (var current = ex; current is not null; current = current.InnerException!)
    {
        if (current is Npgsql.NpgsqlException or Npgsql.PostgresException)
        {
            return true;
        }

        var message = current.Message ?? "";
        if (message.Contains("Exception while reading from stream", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (current.InnerException is null)
        {
            break;
        }
    }

    return false;
}

static (string? UserId, IResult? Error) RequireUser(HttpRequest request, TokenService tokens, string pathUserId)
{
    var headerUserId = tokens.TryGetSubFromAuthorization(request.Headers.Authorization.ToString());
    if (headerUserId is null)
    {
        return (null, Results.Json(
            new ErrorBody
            {
                Code = "missing_auth_context",
                Message = "A valid Bearer token is required."
            },
            statusCode: 401));
    }

    if (!string.Equals(headerUserId, pathUserId, StringComparison.Ordinal))
    {
        return (null, Results.Json(
            new ErrorBody
            {
                Code = "user_id_mismatch",
                Message = "user_id in URL does not match the authenticated user_id."
            },
            statusCode: 403));
    }

    return (headerUserId, null);
}

static (bool AllowAll, HashSet<string> Origins) ParseCorsOrigins(string? envValue)
{
    var raw = envValue?.Trim() ?? "";
    if (raw.Length == 0)
    {
        return (false, new HashSet<string>(StringComparer.Ordinal));
    }

    if (raw == "*")
    {
        return (true, new HashSet<string>(StringComparer.Ordinal));
    }

    var origins = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.Ordinal);
    return (false, origins);
}

static string? ResolveCorsAllowOrigin(string? requestOrigin, (bool AllowAll, HashSet<string> Origins) cors)
{
    if (string.IsNullOrWhiteSpace(requestOrigin))
    {
        return null;
    }

    var trimmed = requestOrigin.Trim();
    if (cors.AllowAll)
    {
        return trimmed;
    }

    return cors.Origins.Contains(trimmed) ? trimmed : null;
}

static void ApplyCors(HttpContext ctx, (bool AllowAll, HashSet<string> Origins) cors)
{
    var allowOrigin = ResolveCorsAllowOrigin(ctx.Request.Headers.Origin.ToString(), cors);
    if (allowOrigin is null)
    {
        return;
    }

    ctx.Response.Headers.Append("Access-Control-Allow-Origin", allowOrigin);
    ctx.Response.Headers.Append("Vary", "Origin");
    ctx.Response.Headers.Append("Access-Control-Allow-Headers", "authorization, content-type");
    ctx.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
}

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
