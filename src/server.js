import { createServer } from "node:http";
import { pathToFileURL } from "node:url";
import { extractUserIdFromHeaders, resolveAuthenticatedUserId } from "./authContext.js";
import { UserStore } from "./userStore.js";

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

function makeDemoToken(userId) {
  return `demo.${userId}`;
}

export function createUserServiceServer({ store }) {
  if (!store) {
    throw new Error("createUserServiceServer requires store.");
  }

  return createServer(async (req, res) => {
    if (req.method === "GET" && req.url === "/health") {
      return sendJson(res, 200, { ok: true, service: "user-service" });
    }

    if (req.method === "POST" && req.url === "/v1/auth/demo-token") {
      try {
        const payload = await parseJsonBody(req);
        const fromBody =
          typeof payload.user_id === "string" && payload.user_id.trim().length > 0
            ? payload.user_id.trim()
            : null;
        const userId = fromBody || `demo-user-${Date.now()}`;
        const user = await store.getOrCreateUser({
          userId,
          phone: payload.phone,
          email: payload.email
        });
        return sendJson(res, 200, { token: makeDemoToken(userId), user });
      } catch (error) {
        const body = JSON.parse(error.message);
        return sendJson(res, 400, body);
      }
    }

    const userIdFromPath = parseUserPath(req.url || "");
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
  const store = new UserStore();
  await store.init();
  const server = createUserServiceServer({ store });
  server.listen(DEFAULT_PORT, () => {
    // eslint-disable-next-line no-console
    console.log(`User service listening on ${DEFAULT_PORT}`);
  });
}
