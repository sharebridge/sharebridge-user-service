using Npgsql;

namespace SharingBridge.UserService;

/// <summary>Retries transient Supabase / network failures (timeouts, broken connections).</summary>
public static class DbRetry
{
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        ILogger? logger = null,
        int maxAttempts = 3,
        CancellationToken ct = default)
    {
        Exception? last = null;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await action(ct);
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < maxAttempts)
            {
                last = ex;
                var delayMs = 200 * attempt * attempt;
                logger?.LogWarning(
                    ex,
                    "Transient DB failure (attempt {Attempt}/{Max}); retrying in {DelayMs}ms",
                    attempt,
                    maxAttempts,
                    delayMs);
                await Task.Delay(delayMs, ct);
            }
        }

        throw last ?? new InvalidOperationException("DB retry exhausted without an exception.");
    }

    public static async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        ILogger? logger = null,
        int maxAttempts = 3,
        CancellationToken ct = default)
    {
        await ExecuteAsync(async token =>
        {
            await action(token);
            return true;
        }, logger, maxAttempts, ct);
    }

    public static bool IsTransient(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException!)
        {
            if (current is TimeoutException)
            {
                return true;
            }

            if (current is NpgsqlException npgsql)
            {
                // Transient: timeouts, connection breaks; not constraint/SQL state errors.
                if (npgsql.InnerException is TimeoutException or IOException)
                {
                    return true;
                }

                var message = npgsql.Message ?? "";
                if (message.Contains("Exception while reading from stream", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("Exception while writing to stream", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("Failed to connect", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("the database system is starting up", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("too many connections", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (current is IOException)
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
}
