/**
 * Optional CORS for browser clients (sharingbridge-web-app sign-in).
 * Set WEB_CORS_ORIGINS to a comma-separated list, or * for local dev only.
 */
export function parseCorsOrigins(envValue = process.env.WEB_CORS_ORIGINS) {
  const raw = typeof envValue === "string" ? envValue.trim() : "";
  if (!raw) {
    return { allowAll: false, origins: new Set() };
  }
  if (raw === "*") {
    return { allowAll: true, origins: new Set() };
  }
  const origins = new Set(
    raw
      .split(",")
      .map((part) => part.trim())
      .filter(Boolean)
  );
  return { allowAll: false, origins };
}

export function resolveCorsAllowOrigin(requestOrigin, corsConfig) {
  if (!requestOrigin || typeof requestOrigin !== "string") {
    return null;
  }
  const trimmed = requestOrigin.trim();
  if (!trimmed) {
    return null;
  }
  if (corsConfig.allowAll) {
    return trimmed;
  }
  if (corsConfig.origins.has(trimmed)) {
    return trimmed;
  }
  return null;
}

export function applyCorsHeaders(req, res, corsConfig) {
  const allowOrigin = resolveCorsAllowOrigin(req.headers.origin, corsConfig);
  if (!allowOrigin) {
    return false;
  }
  res.setHeader("Access-Control-Allow-Origin", allowOrigin);
  res.setHeader("Vary", "Origin");
  res.setHeader(
    "Access-Control-Allow-Headers",
    "authorization, content-type"
  );
  res.setHeader(
    "Access-Control-Allow-Methods",
    "GET, POST, PUT, DELETE, OPTIONS"
  );
  return true;
}

export function handleCorsPreflight(req, res, corsConfig) {
  if (req.method !== "OPTIONS") {
    return false;
  }
  const allowed = applyCorsHeaders(req, res, corsConfig);
  res.writeHead(allowed ? 204 : 403);
  res.end();
  return true;
}
