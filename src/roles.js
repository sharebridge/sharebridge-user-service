export const ROLE_DONOR = "donor";
export const ROLE_COORDINATOR = "coordinator";

export function isValidRole(role) {
  return role === ROLE_DONOR || role === ROLE_COORDINATOR;
}

export function isMobileClientType(clientType) {
  return (
    clientType === "android" || clientType === "ios" || clientType === "mobile"
  );
}

/** JWT `role` for this sign-in: client picks hat; `roles` in token stays the full set. */
export function roleForClientType(clientType, roles) {
  if (isMobileClientType(clientType)) {
    return ROLE_DONOR;
  }
  if (clientType === "web") {
    return ROLE_COORDINATOR;
  }
  return roles.includes(ROLE_COORDINATOR) ? ROLE_COORDINATOR : ROLE_DONOR;
}

export function clientRoleError(
  clientType,
  roles,
  { allowWebDashboardAnyUser = false } = {}
) {
  if (isMobileClientType(clientType)) {
    if (!roles.includes(ROLE_DONOR)) {
      return {
        code: "wrong_client_role",
        message: "This account cannot use the mobile donor app."
      };
    }
    return null;
  }
  if (clientType === "web") {
    if (!roles.includes(ROLE_COORDINATOR)) {
      if (allowWebDashboardAnyUser && roles.includes(ROLE_DONOR)) {
        return null;
      }
      return {
        code: "wrong_client_role",
        message:
          "This Google account is not a coordinator. Use the mobile app as a donor."
      };
    }
    return null;
  }
  return null;
}
