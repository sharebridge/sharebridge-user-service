import test from "node:test";
import assert from "node:assert/strict";
import {
  extractUserIdFromHeaders,
  resolveAuthenticatedUserId
} from "../src/authContext.js";
import { mintAuthToken } from "../src/tokenService.js";

const AUTH_KEYS = [
  "AUTH_TOKEN_SECRET",
  "AUTH_TOKEN_ISSUER",
  "AUTH_TOKEN_AUDIENCE",
  "AUTH_TOKEN_TTL_SECONDS"
];

function clearAuthEnv() {
  for (const key of AUTH_KEYS) {
    delete process.env[key];
  }
}

test.beforeEach(() => {
  clearAuthEnv();
});

test.afterEach(() => {
  clearAuthEnv();
});

test("extractUserIdFromHeaders returns sub for valid Bearer token", () => {
  process.env.AUTH_TOKEN_SECRET = "ctx-test-secret";
  const token = mintAuthToken("user-42", {
    ttlSeconds: 86400
  });
  const userId = extractUserIdFromHeaders({
    authorization: `Bearer ${token}`
  });
  assert.equal(userId, "user-42");
});

test("extractUserIdFromHeaders returns null for invalid token", () => {
  process.env.AUTH_TOKEN_SECRET = "ctx-test-secret";
  assert.equal(
    extractUserIdFromHeaders({
      authorization: "Bearer totally-invalid-jwt"
    }),
    null
  );
});

test("extractUserIdFromHeaders returns null when Authorization missing", () => {
  assert.equal(extractUserIdFromHeaders({}), null);
  assert.equal(extractUserIdFromHeaders(null), null);
});

test("extractUserIdFromHeaders returns null for non-Bearer scheme", () => {
  process.env.AUTH_TOKEN_SECRET = "ctx-test-secret";
  const token = mintAuthToken("user", { ttlSeconds: 86400 });
  assert.equal(
    extractUserIdFromHeaders({ authorization: `Basic ${token}` }),
    null
  );
});

test("resolveAuthenticatedUserId returns 401 when header user missing", () => {
  const result = resolveAuthenticatedUserId({
    headerUserId: null,
    supplied: "ignored"
  });
  assert.equal(result.userId, null);
  assert.equal(result.error?.status, 401);
  assert.equal(result.error?.body?.code, "missing_auth_context");
});

test("resolveAuthenticatedUserId rejects header vs supplied mismatch", () => {
  const result = resolveAuthenticatedUserId({
    headerUserId: "alice",
    supplied: "bob"
  });
  assert.equal(result.userId, null);
  assert.equal(result.error?.status, 403);
  assert.equal(result.error?.body?.code, "user_id_mismatch");
});

test("resolveAuthenticatedUserId accepts header-only identity", () => {
  const result = resolveAuthenticatedUserId({
    headerUserId: "alice",
    supplied: undefined
  });
  assert.equal(result.userId, "alice");
  assert.equal(result.error, null);
});
