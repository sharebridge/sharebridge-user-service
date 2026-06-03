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
    if (roles.includes(ROLE_COORDINATOR)) {
      return ROLE_COORDINATOR;
    }
    if (roles.includes(ROLE_DONOR)) {
      return ROLE_DONOR;
    }
    return ROLE_DONOR;
  }
  return roles.includes(ROLE_COORDINATOR) ? ROLE_COORDINATOR : ROLE_DONOR;
}

export function clientRoleError(clientType, roles) {
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
    if (!roles.includes(ROLE_DONOR) && !roles.includes(ROLE_COORDINATOR)) {
      return {
        code: "wrong_client_role",
        reason: "no_app_role",
        message:
          "This Google account is not set up for SharingBridge yet. Use the mobile app first or ask an admin for coordinator access."
      };
    }
    return null;
  }
  return null;
}
