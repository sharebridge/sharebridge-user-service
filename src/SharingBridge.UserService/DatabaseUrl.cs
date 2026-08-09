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

        var port = uri.IsDefaultPort ? 5432 : uri.Port;
        // Supabase transaction pooler (6543) routinely hangs Npgsql ("Timeout during reading").
        // Session mode on the same host uses 5432 and works with persistent .NET services.
        if (port == 6543 &&
            uri.Host.Contains("pooler.supabase.com", StringComparison.OrdinalIgnoreCase))
        {
            port = 5432;
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = port,
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

    public static string DescribeForLog(string normalizedConnectionString)
    {
        try
        {
            var b = new NpgsqlConnectionStringBuilder(normalizedConnectionString);
            return $"Host={b.Host};Port={b.Port};Database={b.Database};Username={b.Username};SSL Mode={b.SslMode};Pooling={b.Pooling};Max Auto Prepare={b.MaxAutoPrepare}";
        }
        catch
        {
            return "(unparseable connection string)";
        }
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

            // Transaction pooler does not support prepared statements; disable anyway.
            builder.MaxAutoPrepare = 0;
            builder.Timeout = Math.Max(builder.Timeout, 30);
            builder.CommandTimeout = Math.Max(builder.CommandTimeout, 30);
            // Fresh connections avoid stale Supavisor pool entries after deploy SELECT 1.
            builder.Pooling = false;
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
