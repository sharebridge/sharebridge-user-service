import { verifyAuthToken } from "./tokenService.js";

function readBearerToken(authorizationHeader) {
  if (typeof authorizationHeader !== "string") return null;
  const trimmed = authorizationHeader.trim();
  if (!trimmed.toLowerCase().startsWith("bearer ")) return null;
  return trimmed.slice(7).trim();
}

export function extractUserIdFromHeaders(headers) {
  if (!headers) return null;

  const token = readBearerToken(headers.authorization);
  if (token) {
    try {
      return verifyAuthToken(token).sub;
    } catch {
      return null;
    }
  }
  return null;
}

export function resolveAuthenticatedUserId({ headerUserId, supplied }) {
  if (!headerUserId) {
    return {
      userId: null,
      error: {
        status: 401,
        body: {
          code: "missing_auth_context",
          message: "A valid Bearer token is required."
        }
      }
    };
  }

  if (headerUserId && supplied && headerUserId !== supplied) {
    return {
      userId: null,
      error: {
        status: 403,
        body: {
          code: "user_id_mismatch",
          message: "user_id in URL does not match the authenticated user_id."
        }
      }
    };
  }

  return { userId: headerUserId, error: null };
}
