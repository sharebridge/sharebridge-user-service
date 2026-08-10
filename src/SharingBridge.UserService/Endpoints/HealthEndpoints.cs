namespace SharingBridge.UserService;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", (IConfiguration config, DataAccessOptions dataAccessOptions) =>
        {
            var logLevel = (config["LOG_LEVEL"] ?? "warn").Trim().ToLowerInvariant();
            return Results.Json(new
            {
                ok = true,
                service = "user-service",
                config = new
                {
                    service = "user-service",
                    database_url_set = !string.IsNullOrWhiteSpace(
                        config["DATABASE_URL"] ?? Environment.GetEnvironmentVariable("DATABASE_URL")),
                    web_cors_origins_set = !string.IsNullOrWhiteSpace(config["WEB_CORS_ORIGINS"]),
                    google_client_id_web_set = !string.IsNullOrWhiteSpace(config["GOOGLE_CLIENT_ID_WEB"]),
                    google_client_id_android_set = !string.IsNullOrWhiteSpace(config["GOOGLE_CLIENT_ID_ANDROID"]),
                    auth_token_secret_set = !string.IsNullOrWhiteSpace(config["AUTH_TOKEN_SECRET"]),
                    log_level = logLevel,
                    data_access = dataAccessOptions.ToPublicConfig()
                }
            });
        });

        return app;
    }
}
