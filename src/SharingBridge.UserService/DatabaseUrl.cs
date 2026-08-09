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
            return ApplyHostedDefaults(new NpgsqlConnectionStringBuilder(value)).ConnectionString;
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

        return ApplyHostedDefaults(builder).ConnectionString;
    }

    /// <summary>
    /// Settings that keep Npgsql working with Supabase Supavisor / Render free tier.
    /// </summary>
    private static NpgsqlConnectionStringBuilder ApplyHostedDefaults(NpgsqlConnectionStringBuilder builder)
    {
        var host = builder.Host ?? "";
        var isLocal =
            host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host is "127.0.0.1" or "::1";

        if (!isLocal)
        {
            if (builder.SslMode is SslMode.Disable or SslMode.Allow or SslMode.Prefer)
            {
                builder.SslMode = SslMode.Require;
            }

            // Transaction pooler (6543) does not support prepared statements.
            builder.MaxAutoPrepare = 0;
            // Supavisor often closes the socket if GSS negotiation is attempted.
            builder.GssEncryptionMode = GssEncryptionMode.Disable;
            builder.Timeout = Math.Max(builder.Timeout, 60);
            builder.CommandTimeout = Math.Max(builder.CommandTimeout, 60);
            builder.MaxPoolSize = Math.Min(builder.MaxPoolSize == 0 ? 5 : builder.MaxPoolSize, 5);
            builder.Multiplexing = false;
        }

        return builder;
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
            _ => SslMode.Require
        };
}
