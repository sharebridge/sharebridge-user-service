export const ROLE_INITIATOR = "initiator";
/** @deprecated DB/JWT legacy alias — treated as initiator capability */
export const ROLE_DONOR = "donor";
export const ROLE_COORDINATOR = "coordinator";

export function isInitiatorRole(role) {
  return role === ROLE_INITIATOR || role === ROLE_DONOR;
}

export function rolesIncludeInitiator(roles) {
  return Array.isArray(roles) && roles.some((role) => isInitiatorRole(role));
}

export function isValidRole(role) {
  return isInitiatorRole(role) || role === ROLE_COORDINATOR;
}

export function isMobileClientType(clientType) {
  return (
    clientType === "android" || clientType === "ios" || clientType === "mobile"
  );
}

/** JWT `role` for this sign-in: client picks hat; `roles` in token stays the full set. */
export function roleForClientType(clientType, roles) {
  if (isMobileClientType(clientType)) {
    return ROLE_INITIATOR;
  }
  if (clientType === "web") {
    if (roles.includes(ROLE_COORDINATOR)) {
      return ROLE_COORDINATOR;
    }
    if (rolesIncludeInitiator(roles)) {
      return ROLE_INITIATOR;
    }
    return ROLE_INITIATOR;
  }
  return roles.includes(ROLE_COORDINATOR) ? ROLE_COORDINATOR : ROLE_INITIATOR;
}

export function clientRoleError(clientType, roles) {
  if (isMobileClientType(clientType)) {
    if (!rolesIncludeInitiator(roles)) {
      return {
        code: "wrong_client_role",
        reason: "no_initiator_role",
        message: "This account cannot use the mobile app."
      };
    }
    return null;
  }
  if (clientType === "web") {
    if (!rolesIncludeInitiator(roles) && !roles.includes(ROLE_COORDINATOR)) {
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
