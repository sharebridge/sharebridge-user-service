using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SharingBridge.UserService.Tests.Endpoints;

public class FallbackRouteTests : TestWebAppFactory
{
    public FallbackRouteTests(WebApplicationFactory<Program> factory) : base(factory)
    {
    }

    [Fact]
    public async Task Unknown_route_returns_404()
    {
        var client = CreateClient();
        var response = await client.GetAsync("/v1/auth/token");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("not_found", json.GetProperty("code").GetString());
    }
}
