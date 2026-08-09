using Npgsql;

namespace SharingBridge.UserService;

/// <summary>
/// Node <c>pg</c> accepts Postgres URIs as-is. Npgsql is stricter — normalize for Supabase/Render.
/// </summary>
public static class DatabaseUrl
{
    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException("DATABASE_URL is empty.");
        }

        var value = raw.Trim().Trim('"').Trim('\'');
        if (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
        {
            value = "postgresql://" + value["postgres://".Length..];
        }

        if (!value.Contains("://", StringComparison.Ordinal))
        {
            // Already key=value form (Host=...;Username=...)
            return value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                "DATABASE_URL looks like a URI but could not be parsed. " +
                "URL-encode special characters in the password, or use Npgsql key=value form.");
        }

        var userInfo = uri.UserInfo.Split(':', 2);
        var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var database = uri.AbsolutePath.Trim('/');
        if (string.IsNullOrEmpty(database))
        {
            database = "postgres";
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = database,
            Username = username,
            Password = password
        };

        var query = uri.Query.TrimStart('?');
        if (!string.IsNullOrEmpty(query))
        {
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                var key = Uri.UnescapeDataString(kv[0]).Trim();
                var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]).Trim() : "";
                if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("ssl mode", StringComparison.OrdinalIgnoreCase))
                {
                    builder.SslMode = ParseSslMode(val);
                }
            }
        }

        // Supabase / most hosted Postgres require TLS.
        if (builder.SslMode == SslMode.Disable &&
            (uri.Host.Contains("supabase", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.Contains("render.com", StringComparison.OrdinalIgnoreCase)))
        {
            builder.SslMode = SslMode.Require;
        }

        if (builder.SslMode == SslMode.Disable && value.Contains("sslmode=require", StringComparison.OrdinalIgnoreCase))
        {
            builder.SslMode = SslMode.Require;
        }

        // Default for remote hosts when sslmode omitted (matches typical Node pg + Supabase).
        if (builder.SslMode == SslMode.Disable &&
            !uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
            uri.Host is not "127.0.0.1")
        {
            builder.SslMode = SslMode.Prefer;
        }

        return builder.ConnectionString;
    }

    private static SslMode ParseSslMode(string value) =>
        value.ToLowerInvariant() switch
        {
            "disable" => SslMode.Disable,
            "allow" => SslMode.Allow,
            "prefer" => SslMode.Prefer,
            "require" => SslMode.Require,
            "verify-ca" => SslMode.VerifyCA,
            "verify-full" => SslMode.VerifyFull,
            _ => SslMode.Prefer
        };
}
