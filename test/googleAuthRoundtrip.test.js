import assert from "node:assert/strict";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";
import { ROLE_COORDINATOR, ROLE_DONOR } from "../src/roles.js";
import { createUserServiceServer } from "../src/server.js";
import { UserStore } from "../src/userStore.js";
import { verifyAuthToken } from "../src/tokenService.js";

async function tempStore() {
  const dir = await mkdtemp(path.join(tmpdir(), "sb-user-google-"));
  const store = new UserStore({ storagePath: path.join(dir, "store.json") });
  await store.init();
  return { store, dir };
}

test("POST /v1/auth/google mints donor token for mobile client", async () => {
  const { store, dir } = await tempStore();
  const googleAuthVerifier = {
    audiences: ["test-client"],
    async verifyIdToken() {
      return {
        googleSub: "google-sub-donor",
        email: "donor@example.com",
        emailVerified: true,
        name: "Donor User",
        picture: null
      };
    }
  };
  const server = createUserServiceServer({
    store,
    googleAuthVerifier
  });
  await new Promise((resolve) => server.listen(0, resolve));
  const { port } = server.address();

  try {
    const response = await fetch(`http://127.0.0.1:${port}/v1/auth/google`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        id_token: "fake",
        client_type: "android"
      })
    });
    assert.equal(response.status, 200);
    const body = await response.json();
    assert.equal(body.user.role, ROLE_DONOR);
    const payload = verifyAuthToken(body.token);
    assert.equal(payload.role, ROLE_DONOR);
  } finally {
    server.close();
    await rm(dir, { recursive: true, force: true });
  }
});

test("POST /v1/auth/google allows donor on web when allowWebDashboardAnyUser", async () => {
  const { store, dir } = await tempStore();
  const googleAuthVerifier = {
    audiences: ["test-client"],
    async verifyIdToken() {
      return {
        googleSub: "google-sub-donor-mvp",
        email: "donor-mvp@example.com",
        emailVerified: true,
        name: "MVP Donor",
        picture: null
      };
    }
  };
  const server = createUserServiceServer({
    store,
    googleAuthVerifier,
    allowWebDashboardAnyUser: true
  });
  await new Promise((resolve) => server.listen(0, resolve));
  const { port } = server.address();

  try {
    const response = await fetch(`http://127.0.0.1:${port}/v1/auth/google`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        id_token: "fake",
        client_type: "web"
      })
    });
    assert.equal(response.status, 200);
    const body = await response.json();
    assert.equal(body.user.role, ROLE_COORDINATOR);
    const payload = verifyAuthToken(body.token);
    assert.equal(payload.role, ROLE_COORDINATOR);
  } finally {
    server.close();
    await rm(dir, { recursive: true, force: true });
  }
});

test("POST /v1/auth/google rejects donor on web client", async () => {
  const { store, dir } = await tempStore();
  const googleAuthVerifier = {
    audiences: ["test-client"],
    async verifyIdToken() {
      return {
        googleSub: "google-sub-donor2",
        email: "donor2@example.com",
        emailVerified: true,
        name: null,
        picture: null
      };
    }
  };
  const server = createUserServiceServer({
    store,
    googleAuthVerifier
  });
  await new Promise((resolve) => server.listen(0, resolve));
  const { port } = server.address();

  try {
    const response = await fetch(`http://127.0.0.1:${port}/v1/auth/google`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        id_token: "fake",
        client_type: "web"
      })
    });
    assert.equal(response.status, 403);
    const body = await response.json();
    assert.equal(body.code, "wrong_client_role");
  } finally {
    server.close();
    await rm(dir, { recursive: true, force: true });
  }
});

test("POST /v1/auth/google accepts access_token for web account picker sign-in", async () => {
  const { store, dir } = await tempStore();
  const googleAuthVerifier = {
    audiences: ["test-client"],
    async verifyAccessToken() {
      return {
        googleSub: "google-sub-switch",
        email: "other@example.com",
        emailVerified: true,
        name: "Other Coordinator",
        picture: null
      };
    }
  };
  const server = createUserServiceServer({
    store,
    googleAuthVerifier
  });
  await new Promise((resolve) => server.listen(0, resolve));
  const { port } = server.address();

  try {
    await store.ensureRole(
      (
        await store.findOrCreateGoogleUser({
          googleSub: "google-sub-switch",
          email: "other@example.com",
          name: "Other Coordinator",
          picture: null
        })
      ).id,
      ROLE_COORDINATOR
    );
    const response = await fetch(`http://127.0.0.1:${port}/v1/auth/google`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        access_token: "fake-access",
        client_type: "web"
      })
    });
    assert.equal(response.status, 200);
    const body = await response.json();
    assert.equal(body.user.role, ROLE_COORDINATOR);
    assert.equal(body.user.email, "other@example.com");
  } finally {
    server.close();
    await rm(dir, { recursive: true, force: true });
  }
});

test("POST /v1/auth/google mints donor on mobile when user has donor and coordinator", async () => {
  const { store, dir } = await tempStore();
  const googleAuthVerifier = {
    audiences: ["test-client"],
    async verifyIdToken() {
      return {
        googleSub: "google-sub-both",
        email: "both@example.com",
        emailVerified: true,
        name: "Both Roles",
        picture: null
      };
    }
  };
  const server = createUserServiceServer({
    store,
    googleAuthVerifier
  });
  await new Promise((resolve) => server.listen(0, resolve));
  const { port } = server.address();

  try {
    const user = await store.findOrCreateGoogleUser({
      googleSub: "google-sub-both",
      email: "both@example.com",
      name: "Both Roles",
      picture: null
    });
    await store.ensureRole(user.id, ROLE_COORDINATOR);

    const mobile = await fetch(`http://127.0.0.1:${port}/v1/auth/google`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        id_token: "fake",
        client_type: "android"
      })
    });
    assert.equal(mobile.status, 200);
    const mobileBody = await mobile.json();
    assert.equal(mobileBody.user.role, ROLE_DONOR);
    assert.ok(mobileBody.token);
    const mobileJwt = verifyAuthToken(mobileBody.token);
    assert.equal(mobileJwt.role, ROLE_DONOR);
    assert.ok(mobileJwt.roles.includes(ROLE_COORDINATOR));
    assert.ok(mobileJwt.roles.includes(ROLE_DONOR));

    const web = await fetch(`http://127.0.0.1:${port}/v1/auth/google`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        id_token: "fake",
        client_type: "web"
      })
    });
    assert.equal(web.status, 200);
    const webBody = await web.json();
    assert.equal(webBody.user.role, ROLE_COORDINATOR);
    const webJwt = verifyAuthToken(webBody.token);
    assert.equal(webJwt.role, ROLE_COORDINATOR);
  } finally {
    server.close();
    await rm(dir, { recursive: true, force: true });
  }
});

test("POST /v1/auth/google mints coordinator when user_roles has coordinator", async () => {
  const { store, dir } = await tempStore();
  const googleAuthVerifier = {
    audiences: ["test-client"],
    async verifyIdToken() {
      return {
        googleSub: "google-sub-coord",
        email: "coord@example.com",
        emailVerified: true,
        name: "Coordinator",
        picture: null
      };
    }
  };
  const server = createUserServiceServer({
    store,
    googleAuthVerifier
  });
  await new Promise((resolve) => server.listen(0, resolve));
  const { port } = server.address();

  try {
    const user = await store.findOrCreateGoogleUser({
      googleSub: "google-sub-coord",
      email: "coord@example.com",
      name: "Coordinator",
      picture: null
    });
    await store.ensureRole(user.id, ROLE_COORDINATOR);

    const response = await fetch(`http://127.0.0.1:${port}/v1/auth/google`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        id_token: "fake",
        client_type: "web"
      })
    });
    assert.equal(response.status, 200);
    const body = await response.json();
    assert.equal(body.user.role, ROLE_COORDINATOR);
    const payload = verifyAuthToken(body.token);
    assert.equal(payload.role, ROLE_COORDINATOR);
  } finally {
    server.close();
    await rm(dir, { recursive: true, force: true });
  }
});
