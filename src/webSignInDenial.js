import { envFlagEnabled, isProductionDeployment } from "./deploymentEnv.js";
import { ROLE_COORDINATOR, ROLE_DONOR } from "./roles.js";

/**
 * Web Google sign-in denial with actionable `reason` for clients.
 * Returns null when sign-in should proceed.
 */
export function webSignInDenialIfAny(roles, { allowWebDashboardAnyUser = false } = {}) {
  if (roles.includes(ROLE_COORDINATOR)) {
    return null;
  }
  if (allowWebDashboardAnyUser && roles.includes(ROLE_DONOR)) {
    return null;
  }

  if (!roles.includes(ROLE_DONOR)) {
    return {
      code: "wrong_client_role",
      reason: "no_donor_role",
      message:
        "This Google account is not set up for SharingBridge yet. Use the mobile app first or ask an admin for access."
    };
  }

  const mvpFlagInEnv = envFlagEnabled("ALLOW_WEB_DASHBOARD_ANY_USER");
  if (mvpFlagInEnv && isProductionDeployment()) {
    return {
      code: "wrong_client_role",
      reason: "mvp_disabled_production",
      message:
        "This account can use the mobile donor app, but donor web dashboard access is disabled on this server (DEPLOYMENT_ENV=production). Use the mobile app, sign in with a coordinator account, or point the web app at a staging user-service with DEPLOYMENT_ENV=staging and ALLOW_WEB_DASHBOARD_ANY_USER=true."
    };
  }

  return {
    code: "wrong_client_role",
    reason: "coordinator_required",
    message:
      "This account is for the mobile donor app, not the web coordinator dashboard. Sign in with a coordinator Google account, or enable donor web access on user-service: ALLOW_WEB_DASHBOARD_ANY_USER=true and DEPLOYMENT_ENV=staging or development (not production), then redeploy user-service."
  };
}
