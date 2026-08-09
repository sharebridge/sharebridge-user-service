import { resolveLogLevel } from "./serviceLog.js";

export function buildStartupConfig(env = process.env) {
  return {
    service: "user-service",
    database_url_set: Boolean(env.DATABASE_URL?.trim()),
    web_cors_origins_set: Boolean(env.WEB_CORS_ORIGINS?.trim()),
    google_client_id_web_set: Boolean(env.GOOGLE_CLIENT_ID_WEB?.trim()),
    google_client_id_android_set: Boolean(env.GOOGLE_CLIENT_ID_ANDROID?.trim()),
    auth_token_secret_set: Boolean(env.AUTH_TOKEN_SECRET?.trim())
  };
}

export function collectStartupIssues(config) {
  const issues = [];
  if (!config.database_url_set) {
    issues.push("DATABASE_URL is unset");
  }
  if (!config.web_cors_origins_set) {
    issues.push("WEB_CORS_ORIGINS is unset");
  }
  if (!config.google_client_id_web_set) {
    issues.push("GOOGLE_CLIENT_ID_WEB is unset");
  }
  return issues;
}

export function buildHealthConfig(env = process.env) {
  return {
    ...buildStartupConfig(env),
    log_level: resolveLogLevel(env)
  };
}
