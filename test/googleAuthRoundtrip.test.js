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
