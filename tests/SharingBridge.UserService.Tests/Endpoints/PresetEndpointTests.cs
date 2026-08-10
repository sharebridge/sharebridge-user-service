using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using SharingBridge.UserService;

namespace SharingBridge.UserService.Tests.Endpoints;

public class PresetEndpointTests : TestWebAppFactory
{
    public PresetEndpointTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task Presets_require_bearer()
    {
        var client = CreateClient();
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

        var client = CreateClient();
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
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);

        var response = await client.GetAsync("/v1/users/u_b/donor-presets");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("user_id_mismatch", json.GetProperty("code").GetString());
    }
}
