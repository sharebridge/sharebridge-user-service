namespace SharingBridge.UserService;

/// <summary>
/// Supabase pooler port on <c>*.pooler.supabase.com</c>.
/// Env <c>DB_SUPABASE_POOL_6543_4TR_5432_4SESN</c>: <c>5432</c> (session) or <c>6543</c> (transaction).
/// </summary>
public enum SupabasePoolPort
{
    Session = 5432,
    Transaction = 6543
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
    public const string SupabasePoolPortKey = "DB_SUPABASE_POOL_6543_4TR_5432_4SESN";
    public const string RetryMaxKey = "DB_RETRY_MAX_ATTEMPTS";
    public const string RetryBaseDelayKey = "DB_RETRY_BASE_DELAY_MS";

    public bool Pooling { get; init; } = true;
    public int PoolMinSize { get; init; } = 0;
    public int PoolMaxSize { get; init; } = 5;
    public int ConnectionIdleLifetimeSeconds { get; init; } = 60;
    public int ConnectionTimeoutSeconds { get; init; } = 30;
    public int CommandTimeoutSeconds { get; init; } = 30;
    /// <summary>Default <see cref="SupabasePoolPort.Session"/> (5432) when env is unset.</summary>
    public SupabasePoolPort SupabasePoolPort { get; init; } = SupabasePoolPort.Session;
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
            SupabasePoolPort = ReadSupabasePoolPort(config),
            RetryMaxAttempts = ReadInt(config, RetryMaxKey, 3, min: 1, max: 10),
            RetryBaseDelayMs = ReadInt(config, RetryBaseDelayKey, 200, min: 0, max: 30_000)
        };
    }

    public object ToPublicConfig() => new
    {
        pooling = Pooling,
        pool_min = PoolMinSize,
        pool_max = PoolMaxSize,
        connection_idle_lifetime_seconds = ConnectionIdleLifetimeSeconds,
        timeout_seconds = ConnectionTimeoutSeconds,
        command_timeout_seconds = CommandTimeoutSeconds,
        supabase_pool_6543_4tr_5432_4sesn = (int)SupabasePoolPort,
        retry_max_attempts = RetryMaxAttempts,
        retry_base_delay_ms = RetryBaseDelayMs
    };

    private static SupabasePoolPort ReadSupabasePoolPort(IConfiguration config)
    {
        var raw = FirstNonEmpty(config[SupabasePoolPortKey], Environment.GetEnvironmentVariable(SupabasePoolPortKey));
        if (string.IsNullOrWhiteSpace(raw))
        {
            return SupabasePoolPort.Session;
        }

        var trimmed = raw.Trim();
        if (int.TryParse(trimmed, out var port) &&
            Enum.IsDefined(typeof(SupabasePoolPort), port))
        {
            return (SupabasePoolPort)port;
        }

        throw new InvalidOperationException(
            $"{SupabasePoolPortKey} must be 5432 (session) or 6543 (transaction); got '{trimmed}'.");
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
