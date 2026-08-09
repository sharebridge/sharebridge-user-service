import assert from "node:assert/strict";
import { mock } from "node:test";
import test from "node:test";
import { createGoogleAuthVerifier } from "../src/googleAuth.js";

test("verifyAccessToken uses OpenID userinfo instead of tokeninfo", async () => {
  const fetchMock = mock.method(globalThis, "fetch", async (url, init) => {
    assert.equal(url, "https://openidconnect.googleapis.com/v1/userinfo");
    assert.equal(init?.headers?.Authorization, "Bearer good-access");
    return new Response(
      JSON.stringify({
        sub: "google-sub-1",
        email: "user@example.com",
        email_verified: true,
        name: "Test User",
        picture: "https://example.com/p.png"
      }),
      { status: 200, headers: { "content-type": "application/json" } }
    );
  });

  try {
    const verifier = createGoogleAuthVerifier({ clientIds: ["web-client"] });
    const profile = await verifier.verifyAccessToken("good-access");
    assert.equal(profile.googleSub, "google-sub-1");
    assert.equal(profile.email, "user@example.com");
    assert.equal(profile.emailVerified, true);
    assert.equal(profile.name, "Test User");
    assert.equal(profile.picture, "https://example.com/p.png");
    assert.equal(fetchMock.mock.callCount(), 1);
  } finally {
    fetchMock.mock.restore();
  }
});

test("verifyAccessToken surfaces userinfo HTTP errors", async () => {
  const fetchMock = mock.method(globalThis, "fetch", async () => {
    return new Response(
      JSON.stringify({
        error: "invalid_token",
        error_description: "Invalid Value"
      }),
      { status: 401, headers: { "content-type": "application/json" } }
    );
  });

  try {
    const verifier = createGoogleAuthVerifier({ clientIds: ["web-client"] });
    await assert.rejects(
      () => verifier.verifyAccessToken("bad-access"),
      /Google access token validation failed: Invalid Value/
    );
  } finally {
    fetchMock.mock.restore();
  }
});
