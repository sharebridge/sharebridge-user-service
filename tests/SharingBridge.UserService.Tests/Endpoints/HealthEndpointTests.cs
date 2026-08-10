using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SharingBridge.UserService.Tests.Endpoints;

public class HealthEndpointTests : TestWebAppFactory
{
    public HealthEndpointTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("ok").GetBoolean());
        Assert.Equal("user-service", json.GetProperty("service").GetString());
    }
}
