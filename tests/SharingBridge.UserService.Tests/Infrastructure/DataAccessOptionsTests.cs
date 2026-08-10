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
    public void Reads_supabase_pool_6543_to_5432_flag()
    {
        var options = DataAccessOptions.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DB_SUPABASE_POOL_6543TRANS_5432SESSION"] = "false"
            })
            .Build());

        Assert.False(options.SupabasePool6543TransTo5432Session);

        var cs = DatabaseUrl.Normalize(
            "postgresql://user:pass@aws-0-us-east-1.pooler.supabase.com:6543/postgres",
            options);
        Assert.Contains("Port=6543", cs);
    }
}
