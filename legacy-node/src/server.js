import "dotenv/config";
import { createServer } from "node:http";
import { pathToFileURL } from "node:url";
import { extractUserIdFromHeaders, resolveAuthenticatedUserId } from "./authContext.js";
import {
  applyCorsHeaders,
  handleCorsPreflight,
  parseCorsOrigins
} from "./cors.js";
import { createGoogleAuthVerifier } from "./googleAuth.js";
import { PostgresUserStore } from "./postgresUserStore.js";
import {
  ROLE_DONOR,
  clientRoleError,
  roleForClientType
} from "./roles.js";
import { mintAuthToken } from "./tokenService.js";
import { logListenMessage, logStartupFromIssues } from "./serviceLog.js";
import {
  buildHealthConfig,
  buildStartupConfig,
  collectStartupIssues
} from "./startupConfig.js";
import { normalizeUserServiceApiPath } from "./apiPathAliases.js";

const DEFAULT_PORT = Number(process.env.PORT || 8081);

function sendJson(res, statusCode, body) {
  res.writeHead(statusCode, { "content-type": "application/json" });
  res.end(JSON.stringify(body));
}

function parseJsonBody(req) {
  return new Promise((resolve, reject) => {
    let rawBody = "";
    req.on("data", (chunk) => {
      rawBody += chunk;
    });
    req.on("end", () => {
      try {
        resolve(JSON.parse(rawBody || "{}"));
      } catch {
        reject(
          new Error(JSON.stringify({ code: "invalid_json", message: "Request body must be valid JSON." }))
        );
      }
    });
  });
}

function isNonEmptyString(value) {
  return typeof value === "string" && value.trim().length > 0;
}

function validatePreset(preset, index) {
  if (!preset || typeof preset !== "object") {
    return `presets[${index}] must be an object.`;
  }
  if (typeof preset.restaurant_name !== "string" || !preset.restaurant_name.trim()) {
    return `presets[${index}].restaurant_name must be a non-empty string.`;
  }
  if (typeof preset.order_url !== "string" || !preset.order_url.trim()) {
    return `presets[${index}].order_url must be a non-empty string.`;
  }
  if (!Array.isArray(preset.menu_items) || preset.menu_items.some((item) => typeof item !== "string")) {
    return `presets[${index}].menu_items must be an array of strings.`;
  }
  if (typeof preset.app_name !== "string" || !preset.app_name.trim()) {
    return `presets[${index}].app_name must be a non-empty string.`;
  }
  if (typeof preset.source !== "string" || !preset.source.trim()) {
    return `presets[${index}].source must be a non-empty string.`;
  }
  if (typeof preset.confidence !== "number" || Number.isNaN(preset.confidence)) {
    return `presets[${index}].confidence must be a number.`;
  }
  return null;
}

function parseUserPath(urlPath) {
  const match = /^\/v1\/users\/([^/]+)\/donor-presets$/.exec(urlPath);
  if (!match) return null;
  return decodeURIComponent(match[1]);
}

function parseDonorPresetsDeleteItemPath(urlPath) {
  const match = /^\/v1\/users\/([^/]+)\/donor-presets\/delete-item$/.exec(urlPath);
  if (!match) return null;
  return decodeURIComponent(match[1]);
}

async function loadUserRoles(store, userId) {
  await store.ensureRole(userId, ROLE_DONOR);
  return store.getRolesForUser(userId);
}

export function createUserServiceServer({
  store,
  googleAuthVerifier,
  corsConfig = parseCorsOrigins()
}) {
  if (!store) {
    throw new Error("createUserServiceServer requires store.");
  }
  const googleAuth = googleAuthVerifier ?? createGoogleAuthVerifier();

  return createServer(async (req, res) => {
    req.url = normalizeUserServiceApiPath(req.url);
    applyCorsHeaders(req, res, corsConfig);
    if (handleCorsPreflight(req, res, corsConfig)) {
      return;
    }

    if (req.method === "GET" && req.url === "/health") {
      return sendJson(res, 200, {
        ok: true,
        service: "user-service",
        config: buildHealthConfig()
      });
    }

    if (req.method === "POST" && req.url === "/v1/auth/google") {
      try {
        const payload = await parseJsonBody(req);
        const idToken =
          typeof payload.id_token === "string" ? payload.id_token.trim() : "";
        const accessToken =
          typeof payload.access_token === "string"
            ? payload.access_token.trim()
            : "";
        if (!idToken && !accessToken) {
          return sendJson(res, 400, {
            code: "invalid_request",
            message: "id_token or access_token is required."
          });
        }
        const clientType =
          typeof payload.client_type === "string"
            ? payload.client_type.trim().toLowerCase()
            : "web";
        const googleProfile = accessToken
          ? await googleAuth.verifyAccessToken(accessToken)
          : await googleAuth.verifyIdToken(idToken);
        if (!googleProfile.email) {
          return sendJson(res, 400, {
            code: "invalid_request",
            message: "Google account must expose an email address."
          });
        }
        const user = await store.findOrCreateGoogleUser({
          googleSub: googleProfile.googleSub,
          email: googleProfile.email,
          name: googleProfile.name,
          picture: googleProfile.picture
        });
        const roles = await loadUserRoles(store, user.id);
        const roleError = clientRoleError(clientType, roles);
        if (roleError) {
          return sendJson(res, 403, roleError);
        }
        const role = roleForClientType(clientType, roles);
        const token = mintAuthToken(user.id, { role, roles });
        return sendJson(res, 200, {
          token,
          token_type: "Bearer",
          user: { ...user, role }
        });
      } catch (error) {
        if (error?.message?.includes("invalid_request")) {
          return sendJson(res, 400, {
            code: "invalid_request",
            message: error.message
          });
        }
        return sendJson(res, 401, {
          code: "invalid_google_token",
          message: error?.message || "Google sign-in failed."
        });
      }
    }

    const pathOnly = (req.url || "").split("?")[0];
    const deleteItemUserId = parseDonorPresetsDeleteItemPath(pathOnly);
    if (req.method === "POST" && deleteItemUserId) {
      const headerUserId = extractUserIdFromHeaders(req.headers);
      const { userId, error: authError } = resolveAuthenticatedUserId({
        headerUserId
      });
      if (authError) {
        return sendJson(res, authError.status, authError.body);
      }
      if (userId !== deleteItemUserId) {
        return sendJson(res, 403, {
          code: "user_id_mismatch",
          message: "user_id in URL does not match the authenticated user_id."
        });
      }
      try {
        const payload = await parseJsonBody(req);
        if (!isNonEmptyString(payload.restaurant_name)) {
          return sendJson(res, 400, {
            code: "invalid_request",
            message: "restaurant_name is required."
          });
        }
        if (!isNonEmptyString(payload.order_url)) {
          return sendJson(res, 400, {
            code: "invalid_request",
            message: "order_url is required."
          });
        }
        await store.getOrCreateUser({ userId });
        const presets = await store.deleteDonorPreset(userId, {
          restaurant_name: payload.restaurant_name.trim(),
          order_url: payload.order_url.trim()
        });
        return sendJson(res, 200, { presets });
      } catch (error) {
        const body = JSON.parse(error.message);
        return sendJson(res, 400, body);
      }
    }

    const userIdFromPath = parseUserPath(pathOnly);
    if (userIdFromPath && (req.method === "GET" || req.method === "PUT")) {
      const headerUserId = extractUserIdFromHeaders(req.headers);
      const { userId, error: authError } = resolveAuthenticatedUserId({ headerUserId });
      if (authError) {
        return sendJson(res, authError.status, authError.body);
      }
      if (userId !== userIdFromPath) {
        return sendJson(res, 403, {
          code: "user_id_mismatch",
          message: "user_id in URL does not match the authenticated user_id."
        });
      }

      if (req.method === "GET") {
        const presets = await store.listDonorPresets(userId);
        return sendJson(res, 200, { presets });
      }

      try {
        const payload = await parseJsonBody(req);
        if (!Array.isArray(payload.presets)) {
          return sendJson(res, 400, {
            code: "invalid_request",
            message: "presets must be an array."
          });
        }
        for (let i = 0; i < payload.presets.length; i += 1) {
          const validationError = validatePreset(payload.presets[i], i);
          if (validationError) {
            return sendJson(res, 400, { code: "invalid_request", message: validationError });
          }
        }
        await store.getOrCreateUser({ userId });
        const presets = await store.replaceDonorPresets(userId, payload.presets);
        return sendJson(res, 200, { presets });
      } catch (error) {
        const body = JSON.parse(error.message);
        return sendJson(res, 400, body);
      }
    }

    return sendJson(res, 404, { code: "not_found", message: "Route not found." });
  });
}

const isMainModule =
  process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;
if (isMainModule) {
  const databaseUrl = process.env.DATABASE_URL;
  if (!databaseUrl?.trim()) {
    // eslint-disable-next-line no-console
    console.error("DATABASE_URL is required. See configuration/database.md.");
    process.exit(1);
  }
  const store = await PostgresUserStore.create(databaseUrl);
  await store.init();
  const server = createUserServiceServer({ store });
  server.listen(DEFAULT_PORT, () => {
    const startupConfig = buildStartupConfig();
    logListenMessage(
      console,
      `User service listening on ${DEFAULT_PORT} (PostgreSQL)`
    );
    logStartupFromIssues(startupConfig, collectStartupIssues(startupConfig));
  });
}
