namespace SharingBridge.UserService;

public static class CorsSupport
{
    public static (bool AllowAll, HashSet<string> Origins) ParseOrigins(string? envValue)
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

    public static string? ResolveAllowOrigin(string? requestOrigin, (bool AllowAll, HashSet<string> Origins) cors)
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

    public static void Apply(HttpContext ctx, (bool AllowAll, HashSet<string> Origins) cors)
    {
        var allowOrigin = ResolveAllowOrigin(ctx.Request.Headers.Origin.ToString(), cors);
        if (allowOrigin is null)
        {
            return;
        }

        ctx.Response.Headers.Append("Access-Control-Allow-Origin", allowOrigin);
        ctx.Response.Headers.Append("Vary", "Origin");
        ctx.Response.Headers.Append("Access-Control-Allow-Headers", "authorization, content-type");
        ctx.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
    }
}
