using SharingBridge.UserService;

namespace SharingBridge.UserService.Tests.Models;

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
