import test from "node:test";
import assert from "node:assert/strict";
import {
  mintAuthToken,
  verifyAuthToken
} from "../src/tokenService.js";

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

test("mint and verify round-trip with explicit secret", () => {
  const token = mintAuthToken("donor-xyz", {
    secret: "explicit-test-secret",
    ttlSeconds: 3600
  });
  const payload = verifyAuthToken(token, {
    secret: "explicit-test-secret"
  });
  assert.equal(payload.sub, "donor-xyz");
  assert.equal(payload.iss, "sharingbridge-user-service");
  assert.equal(payload.aud, "sharingbridge-clients");
  assert.ok(payload.exp > payload.iat);
});

test("verify rejects wrong secret", () => {
  const token = mintAuthToken("alice", { secret: "signer-key" });
  assert.throws(
    () => verifyAuthToken(token, { secret: "other-key" }),
    /signature is invalid/
  );
});

test("verify rejects tampered signature", () => {
  const token = mintAuthToken("alice", {
    secret: "k",
    ttlSeconds: 7200
  });
  const [h, p, s] = token.split(".");
  const tampered = `${h}.${p}.${s.slice(0, -1)}${s.endsWith("A") ? "B" : "A"}`;
  assert.throws(
    () => verifyAuthToken(tampered, { secret: "k" }),
    /signature is invalid/
  );
});

test("verify rejects expired token", () => {
  const token = mintAuthToken("alice", {
    secret: "k",
    ttlSeconds: -3600
  });
  assert.throws(() => verifyAuthToken(token, { secret: "k" }), /expired/);
});

test("verify rejects issuer mismatch", () => {
  const token = mintAuthToken("alice", {
    secret: "k",
    issuer: "issuer-a",
    ttlSeconds: 7200
  });
  assert.throws(
    () =>
      verifyAuthToken(token, {
        secret: "k",
        issuer: "issuer-b",
        audience: "sharingbridge-clients"
      }),
    /issuer/
  );
});

test("verify rejects audience mismatch", () => {
  const token = mintAuthToken("alice", {
    secret: "k",
    audience: "aud-a",
    ttlSeconds: 7200
  });
  assert.throws(
    () =>
      verifyAuthToken(token, {
        secret: "k",
        issuer: "sharingbridge-user-service",
        audience: "aud-b"
      }),
    /audience/
  );
});

test("verify rejects malformed token shape", () => {
  assert.throws(() => verifyAuthToken("one.part"), /format/);
  assert.throws(() => verifyAuthToken(""), /required/);
});

test("defaults read from env each call", () => {
  process.env.AUTH_TOKEN_SECRET = "env-secret";
  process.env.AUTH_TOKEN_ISSUER = "my-iss";
  process.env.AUTH_TOKEN_AUDIENCE = "my-aud";
  const token = mintAuthToken("u1", { ttlSeconds: 86400 });
  const payload = verifyAuthToken(token);
  assert.equal(payload.sub, "u1");
  assert.equal(payload.iss, "my-iss");
  assert.equal(payload.aud, "my-aud");
});
