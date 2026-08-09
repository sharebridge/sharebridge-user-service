namespace SharingBridge.UserService;

public static class Roles
{
    public const string Initiator = "initiator";
    public const string Donor = "donor";
    public const string Coordinator = "coordinator";

    public static bool IsInitiatorRole(string role) =>
        role == Initiator || role == Donor;

    public static bool RolesIncludeInitiator(IEnumerable<string> roles) =>
        roles.Any(IsInitiatorRole);

    public static bool IsMobileClientType(string clientType) =>
        clientType is "android" or "ios" or "mobile";

    public static string RoleForClientType(string clientType, IReadOnlyList<string> roles)
    {
        if (IsMobileClientType(clientType))
        {
            return Initiator;
        }

        if (clientType == "web")
        {
            if (roles.Contains(Coordinator))
            {
                return Coordinator;
            }

            return Initiator;
        }

        return roles.Contains(Coordinator) ? Coordinator : Initiator;
    }

    public static ErrorBody? ClientRoleError(string clientType, IReadOnlyList<string> roles)
    {
        if (IsMobileClientType(clientType))
        {
            if (!RolesIncludeInitiator(roles))
            {
                return new ErrorBody
                {
                    Code = "wrong_client_role",
                    Reason = "no_initiator_role",
                    Message = "This account cannot use the mobile app."
                };
            }

            return null;
        }

        if (clientType == "web")
        {
            if (!RolesIncludeInitiator(roles) && !roles.Contains(Coordinator))
            {
                return new ErrorBody
                {
                    Code = "wrong_client_role",
                    Reason = "no_app_role",
                    Message =
                        "This Google account is not set up for SharingBridge yet. Use the mobile app first or ask an admin for coordinator access."
                };
            }
        }

        return null;
    }
}
