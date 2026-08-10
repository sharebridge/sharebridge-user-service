namespace SharingBridge.UserService;

/// <summary>
/// Which Supabase pooler port to use on <c>*.pooler.supabase.com</c>.
/// Env <c>DB_SUPABASE_POOL_6543TRANS_5432SESSION</c>: <c>5432SESSION</c> | <c>6543TRANS</c> | <c>AS_IS</c>.
/// </summary>
public enum SupabasePoolMode
{
    /// <summary>Force session pooler port 5432 (recommended for Npgsql).</summary>
    Session5432 = 0,

    /// <summary>Force transaction pooler port 6543.</summary>
    Transaction6543 = 1,

    /// <summary>Leave the URI port unchanged.</summary>
    AsIs = 2
}

/// <summary>
/// Pool + retry settings from environment / configuration (no secrets).
/// </summary>
public sealed class DataAccessOptions
{
    public const string PoolingKey = "DB_POOLING";
    public const string PoolMinKey = "DB_POOL_MIN";
    public const string PoolMaxKey = "DB_POOL_MAX";
    public const string IdleLifetimeKey = "DB_CONNECTION_IDLE_LIFETIME_SECONDS";
    public const string TimeoutKey = "DB_TIMEOUT_SECONDS";
    public const string CommandTimeoutKey = "DB_COMMAND_TIMEOUT_SECONDS";
    public const string SupabasePoolModeKey = "DB_SUPABASE_POOL_6543TRANS_5432SESSION";
    /// <summary>Previous boolean env name — still accepted as a fallback.</summary>
    public const string SupabasePoolModeKeyLegacy = "DB_REWRITE_SUPABASE_TRANSACTION_PORT";
    public const string RetryMaxKey = "DB_RETRY_MAX_ATTEMPTS";
    public const string RetryBaseDelayKey = "DB_RETRY_BASE_DELAY_MS";

    public const string ModeSession5432 = "5432SESSION";
    public const string ModeTransaction6543 = "6543TRANS";
    public const string ModeAsIs = "AS_IS";

    public bool Pooling { get; init; } = true;
    public int PoolMinSize { get; init; } = 0;
    public int PoolMaxSize { get; init; } = 5;
    public int ConnectionIdleLifetimeSeconds { get; init; } = 60;
    public int ConnectionTimeoutSeconds { get; init; } = 30;
    public int CommandTimeoutSeconds { get; init; } = 30;
    /// <summary>
    /// Supabase pooler mode on <c>*.pooler.supabase.com</c>.
    /// Default <see cref="SupabasePoolMode.Session5432"/> (Npgsql-safe).
    /// </summary>
    public SupabasePoolMode SupabasePoolMode { get; init; } = SupabasePoolMode.Session5432;
    public int RetryMaxAttempts { get; init; } = 3;
    public int RetryBaseDelayMs { get; init; } = 200;

    public static DataAccessOptions FromConfiguration(IConfiguration config)
    {
        return new DataAccessOptions
        {
            Pooling = ReadBool(config, PoolingKey, true),
            PoolMinSize = ReadInt(config, PoolMinKey, 0, min: 0, max: 100),
            PoolMaxSize = ReadInt(config, PoolMaxKey, 5, min: 1, max: 100),
            ConnectionIdleLifetimeSeconds = ReadInt(config, IdleLifetimeKey, 60, min: 1, max: 3600),
            ConnectionTimeoutSeconds = ReadInt(config, TimeoutKey, 30, min: 1, max: 300),
            CommandTimeoutSeconds = ReadInt(config, CommandTimeoutKey, 30, min: 1, max: 300),
            SupabasePoolMode =
                TryReadSupabasePoolMode(config, SupabasePoolModeKey)
                ?? TryReadSupabasePoolMode(config, SupabasePoolModeKeyLegacy)
                ?? SupabasePoolMode.Session5432,
            RetryMaxAttempts = ReadInt(config, RetryMaxKey, 3, min: 1, max: 10),
            RetryBaseDelayMs = ReadInt(config, RetryBaseDelayKey, 200, min: 0, max: 30_000)
        };
    }

    public string SupabasePoolModeValue => SupabasePoolMode switch
    {
        SupabasePoolMode.Transaction6543 => ModeTransaction6543,
        SupabasePoolMode.AsIs => ModeAsIs,
        _ => ModeSession5432
    };

    public object ToPublicConfig() => new
    {
        pooling = Pooling,
        pool_min = PoolMinSize,
        pool_max = PoolMaxSize,
        connection_idle_lifetime_seconds = ConnectionIdleLifetimeSeconds,
        timeout_seconds = ConnectionTimeoutSeconds,
        command_timeout_seconds = CommandTimeoutSeconds,
        supabase_pool_6543trans_5432session = SupabasePoolModeValue,
        retry_max_attempts = RetryMaxAttempts,
        retry_base_delay_ms = RetryBaseDelayMs
    };

    private static SupabasePoolMode? TryReadSupabasePoolMode(IConfiguration config, string key)
    {
        var raw = FirstNonEmpty(config[key], Environment.GetEnvironmentVariable(key));
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var normalized = raw.Trim().ToUpperInvariant().Replace("-", "").Replace("_", "");
        return normalized switch
        {
            "5432SESSION" or "SESSION5432" or "SESSION" => SupabasePoolMode.Session5432,
            "6543TRANS" or "TRANS6543" or "TRANSACTION" or "TRANSACTION6543" => SupabasePoolMode.Transaction6543,
            "ASIS" or "UNCHANGED" or "PASSTHROUGH" => SupabasePoolMode.AsIs,
            // Legacy boolean aliases
            "1" or "TRUE" or "YES" or "ON" => SupabasePoolMode.Session5432,
            "0" or "FALSE" or "NO" or "OFF" => SupabasePoolMode.AsIs,
            _ => null
        };
    }

    private static bool? TryReadBool(IConfiguration config, string key)
    {
        var raw = FirstNonEmpty(config[key], Environment.GetEnvironmentVariable(key));
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "yes" or "on" => true,
            "0" or "false" or "no" or "off" => false,
            _ => null
        };
    }

    private static bool ReadBool(IConfiguration config, string key, bool fallback) =>
        TryReadBool(config, key) ?? fallback;

    private static int ReadInt(IConfiguration config, string key, int fallback, int min, int max)
    {
        var raw = FirstNonEmpty(config[key], Environment.GetEnvironmentVariable(key));
        if (!int.TryParse(raw, out var value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
