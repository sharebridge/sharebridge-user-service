using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SharingBridge.UserService;

namespace SharingBridge.UserService.Tests;

public class ApiContractTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiContractTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("USER_STORE", "memory");
            builder.UseSetting("AUTH_TOKEN_SECRET", "test-secret");
            builder.UseSetting("WEB_CORS_ORIGINS", "*");
            builder.UseSetting("GOOGLE_CLIENT_ID_WEB", "test-web-client.apps.googleusercontent.com");
        });
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("ok").GetBoolean());
        Assert.Equal("user-service", json.GetProperty("service").GetString());
    }

    [Fact]
    public async Task Unknown_route_returns_404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/auth/token");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_found", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Presets_require_bearer()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/users/u_test/donor-presets");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("missing_auth_context", json.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Initiator_presets_alias_and_put_roundtrip()
    {
        var tokens = new TokenService(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AUTH_TOKEN_SECRET"] = "test-secret"
            })
            .Build());
        var userId = "u_preset_test";
        var jwt = tokens.Mint(userId, Roles.Initiator, [Roles.Donor, Roles.Initiator]);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var putBody = new
        {
            presets = new[]
            {
                new
                {
                    restaurant_name = "Kitchen A",
                    order_url = "https://example.com/a",
                    menu_items = new[] { "Meal" },
                    app_name = "swiggy",
                    source = "manual",
                    confidence = 0.9
                },
                new
                {
                    restaurant_name = "Kitchen A",
                    order_url = "https://example.com/a",
                    menu_items = new[] { "Meal 2" },
                    app_name = "swiggy",
                    source = "manual",
                    confidence = 0.95
                }
            }
        };

        var put = await client.PutAsJsonAsync($"/v1/users/{userId}/initiator-presets", putBody);
        put.EnsureSuccessStatusCode();
        var putJson = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, putJson.GetProperty("presets").GetArrayLength());

        var get = await client.GetAsync($"/v1/users/{userId}/donor-presets");
        get.EnsureSuccessStatusCode();
        var getJson = await get.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, getJson.GetProperty("presets").GetArrayLength());

        var del = await client.PostAsJsonAsync(
            $"/v1/users/{userId}/donor-presets/delete-item",
            new { restaurant_name = "Kitchen A", order_url = "https://example.com/a" });
        del.EnsureSuccessStatusCode();
        var delJson = await del.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, delJson.GetProperty("presets").GetArrayLength());
    }

    [Fact]
    public async Task User_id_mismatch_returns_403()
    {
        var tokens = new TokenService(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AUTH_TOKEN_SECRET"] = "test-secret"
            })
            .Build());
        var jwt = tokens.Mint("u_a", Roles.Initiator, [Roles.Donor]);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/v1/users/u_b/donor-presets");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("user_id_mismatch", json.GetProperty("code").GetString());
    }
}

public class RolesTests
{
    [Fact]
    public void Mobile_requires_initiator_and_mints_initiator()
    {
        var err = Roles.ClientRoleError("mobile", [Roles.Coordinator]);
        Assert.NotNull(err);
        Assert.Equal("no_initiator_role", err!.Reason);

        Assert.Null(Roles.ClientRoleError("android", [Roles.Donor]));
        Assert.Equal(Roles.Initiator, Roles.RoleForClientType("ios", [Roles.Donor, Roles.Coordinator]));
    }

    [Fact]
    public void Web_prefers_coordinator()
    {
        Assert.Equal(
            Roles.Coordinator,
            Roles.RoleForClientType("web", [Roles.Donor, Roles.Coordinator]));
        Assert.Equal(Roles.Initiator, Roles.RoleForClientType("web", [Roles.Donor]));
    }
}

public class TokenServiceTests
{
    [Fact]
    public void Mint_and_verify_roundtrip()
    {
        var svc = new TokenService(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AUTH_TOKEN_SECRET"] = "roundtrip-secret",
                ["AUTH_TOKEN_ISSUER"] = "sharingbridge-user-service",
                ["AUTH_TOKEN_AUDIENCE"] = "sharingbridge-clients"
            })
            .Build());

        var token = svc.Mint("u_abc", Roles.Coordinator, [Roles.Donor, Roles.Coordinator]);
        var sub = svc.TryGetSubFromAuthorization($"Bearer {token}");
        Assert.Equal("u_abc", sub);
    }
}

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
    public void Rewrites_supabase_transaction_pooler_to_session_port()
    {
        var cs = DatabaseUrl.Normalize(
            "postgresql://user:pass@aws-0-us-east-1.pooler.supabase.com:6543/postgres");
        Assert.Contains("Port=5432", cs);
        Assert.Contains("Pooling=True", cs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Maximum Pool Size=5", cs, StringComparison.OrdinalIgnoreCase);
    }
}

public class DbRetryTests
{
    [Fact]
    public async Task Retries_transient_failure_then_succeeds()
    {
        var attempts = 0;
        var result = await DbRetry.ExecuteAsync(async _ =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new TimeoutException("Timeout during reading attempt");
            }

            return "ok";
        }, maxAttempts: 3);

        Assert.Equal("ok", result);
        Assert.Equal(3, attempts);
    }
}
