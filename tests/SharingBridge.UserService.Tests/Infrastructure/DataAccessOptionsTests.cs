using Microsoft.Extensions.Configuration;
using SharingBridge.UserService;

namespace SharingBridge.UserService.Tests.Infrastructure;

public class DataAccessOptionsTests
{
    [Fact]
    public void Respects_pool_options_from_configuration()
    {
        var options = DataAccessOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_POOLING"] = "false",
                ["DB_POOL_MAX"] = "2",
                ["DB_RETRY_MAX_ATTEMPTS"] = "5",
                ["DB_RETRY_BASE_DELAY_MS"] = "50"
            })
            .Build());

        Assert.False(options.Pooling);
        Assert.Equal(2, options.PoolMaxSize);
        Assert.Equal(5, options.RetryMaxAttempts);
        Assert.Equal(50, options.RetryBaseDelayMs);

        var cs = DatabaseUrl.Normalize(
            "postgresql://user:pass@db.example.com:5432/postgres",
            options);
        Assert.Contains("Pooling=False", cs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reads_supabase_pool_port_5432_or_6543()
    {
        var session = DataAccessOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_SUPABASE_POOL_6543_4TR_5432_4SESN"] = "5432"
            })
            .Build());
        Assert.Equal(SupabasePoolPort.Session, session.SupabasePoolPort);

        var trans = DataAccessOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_SUPABASE_POOL_6543_4TR_5432_4SESN"] = "6543"
            })
            .Build());
        Assert.Equal(SupabasePoolPort.Transaction, trans.SupabasePoolPort);

        var cs = DatabaseUrl.Normalize(
            "postgresql://user:pass@aws-0-us-east-1.pooler.supabase.com:5432/postgres",
            trans);
        Assert.Contains("Port=6543", cs);
    }

    [Fact]
    public void Invalid_supabase_pool_port_fails_fast()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DataAccessOptions.FromConfiguration(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["DB_SUPABASE_POOL_6543_4TR_5432_4SESN"] = "9999"
                })
                .Build()));

        Assert.Contains("must be 5432", ex.Message, StringComparison.Ordinal);
        Assert.Contains("9999", ex.Message, StringComparison.Ordinal);
    }
}
