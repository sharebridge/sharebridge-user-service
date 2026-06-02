/**
 * Production guard for dev/MVP unlock flags.
 * Flags are ignored in production even when env says true.
 */

/** Sign in with a user id instead of Google (POST /v1/auth/token). */
export const ENV_BYPASS_GOOGLE_SIGN_IN = "BYPASS_GOOGLE_SIGN_IN";

export function deploymentEnvLabel() {
  const explicit = process.env.DEPLOYMENT_ENV?.trim().toLowerCase();
  return explicit || null;
}

export function isProductionDeployment() {
  const explicit = deploymentEnvLabel();
  if (explicit === "production") return true;
  if (
    explicit === "development" ||
    explicit === "staging" ||
    explicit === "local"
  ) {
    return false;
  }
  return (
    process.env.NODE_ENV === "production" && process.env.RENDER === "true"
  );
}

export function envFlagEnabled(name) {
  const value = process.env[name];
  return value === "1" || value === "true";
}

/** Dev/MVP unlock: only when flag is set and deployment is not production. */
export function devUnlockFlagEnabled(name) {
  if (isProductionDeployment()) return false;
  return envFlagEnabled(name);
}

export function googleSignInBypassEnabled() {
  return devUnlockFlagEnabled(ENV_BYPASS_GOOGLE_SIGN_IN);
}

export function webDashboardAnyUserEnabled() {
  return devUnlockFlagEnabled("ALLOW_WEB_DASHBOARD_ANY_USER");
}

export function warnIgnoredUnlockFlags() {
  if (!isProductionDeployment()) return;
  for (const name of [ENV_BYPASS_GOOGLE_SIGN_IN, "ALLOW_WEB_DASHBOARD_ANY_USER"]) {
    if (envFlagEnabled(name)) {
      console.warn(
        `[user-service] ${name}=true is ignored in production (DEPLOYMENT_ENV=production or Render NODE_ENV=production).`
      );
    }
  }
}
