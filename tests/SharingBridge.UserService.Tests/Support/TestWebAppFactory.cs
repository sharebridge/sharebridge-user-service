using Microsoft.AspNetCore.Mvc.Testing;

namespace SharingBridge.UserService.Tests;

/// <summary>Shared in-memory host for endpoint contract tests.</summary>
public abstract class TestWebAppFactory : IClassFixture<WebApplicationFactory<Program>>
{
    protected WebApplicationFactory<Program> Factory { get; }

    protected TestWebAppFactory(WebApplicationFactory<Program> factory)
    {
        Factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("USER_STORE", "memory");
            builder.UseSetting("AUTH_TOKEN_SECRET", "test-secret");
            builder.UseSetting("WEB_CORS_ORIGINS", "*");
            builder.UseSetting("GOOGLE_CLIENT_ID_WEB", "test-web-client.apps.googleusercontent.com");
        });
    }

    protected HttpClient CreateClient() => Factory.CreateClient();
}
