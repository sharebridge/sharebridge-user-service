using SharingBridge.UserService;

namespace SharingBridge.UserService.Tests.Infrastructure;

public class DatabaseUrlTests
{
    [Fact]
    public void Normalizes_postgres_uri_and_strips_quotes()
    {
        var cs = DatabaseUrl.Normalize(
            "\"postgres://user:p%40ss@db.example.com:6543/postgres?sslmode=require\"");
        Assert.Contains("Host=db.example.com", cs);
        Assert.Contains("Port=6543", cs);
        Assert.Contains("Username=user", cs);
        Assert.Contains("Password=p@ss", cs);
        Assert.Contains("Database=postgres", cs);
        Assert.Contains("SSL Mode=Require", cs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rewrites_supabase_pooler_to_session_port_by_default()
    {
        var cs = DatabaseUrl.Normalize(
            "postgresql://user:pass@aws-0-us-east-1.pooler.supabase.com:6543/postgres",
            new DataAccessOptions
            {
                Pooling = true,
                PoolMaxSize = 5,
                SupabasePoolPort = SupabasePoolPort.Session
            });
        Assert.Contains("Port=5432", cs);
        Assert.Contains("Pooling=True", cs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Maximum Pool Size=5", cs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Can_force_supabase_transaction_pooler_port()
    {
        var cs = DatabaseUrl.Normalize(
            "postgresql://user:pass@aws-0-us-east-1.pooler.supabase.com:5432/postgres",
            new DataAccessOptions { SupabasePoolPort = SupabasePoolPort.Transaction });
        Assert.Contains("Port=6543", cs);
    }
}
