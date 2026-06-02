/**
 * Production guard for dev/MVP unlock flags (ALLOW_DEV_TOKEN_MINT, ALLOW_WEB_DASHBOARD_ANY_USER).
 * Flags are ignored when this returns true, even if set in the environment.
 */

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

export function webDashboardAnyUserEnabled() {
  return devUnlockFlagEnabled("ALLOW_WEB_DASHBOARD_ANY_USER");
}

export function warnIgnoredUnlockFlags() {
  if (!isProductionDeployment()) return;
  for (const name of [
    "ALLOW_DEV_TOKEN_MINT",
    "ALLOW_WEB_DASHBOARD_ANY_USER"
  ]) {
    if (envFlagEnabled(name)) {
      console.warn(
        `[user-service] ${name}=true is ignored in production (DEPLOYMENT_ENV=production or Render NODE_ENV=production).`
      );
    }
  }
}
