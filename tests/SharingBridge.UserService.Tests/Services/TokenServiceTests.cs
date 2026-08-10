using Microsoft.Extensions.Configuration;
using SharingBridge.UserService;

namespace SharingBridge.UserService.Tests.Services;

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
