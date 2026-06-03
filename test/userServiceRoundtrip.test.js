import test from "node:test";
import assert from "node:assert/strict";
import os from "node:os";
import path from "node:path";
import { mkdir } from "node:fs/promises";
import { verifyAuthToken, mintAuthToken } from "../src/tokenService.js";
import { createUserServiceServer } from "../src/server.js";
import { UserStore } from "../src/userStore.js";

async function startServer() {
  const dir = path.join(os.tmpdir(), `user-service-${Date.now()}`);
  await mkdir(dir, { recursive: true });
  const store = new UserStore({ storagePath: path.join(dir, "store.json") });
  await store.init();
  const server = createUserServiceServer({ store });
  await new Promise((resolve) => server.listen(0, resolve));
  const address = server.address();
  const baseUrl = `http://127.0.0.1:${address.port}`;
  return {
    baseUrl,
    close: async () => {
      await new Promise((resolve, reject) => {
        server.close((error) => (error ? reject(error) : resolve()));
      });
    }
  };
}

function issueToken(userId, role = "donor") {
  return mintAuthToken(userId, { role });
}

test("mintAuthToken produces verifiable donor claims", () => {
  const token = mintAuthToken("donor-001", { role: "donor" });
  const claims = verifyAuthToken(token);
  assert.equal(claims.sub, "donor-001");
  assert.equal(claims.role, "donor");
});

test("upserts donor presets and returns deduped set", async () => {
  const app = await startServer();
  try {
    const token = issueToken("donor-abc");
    const putResponse = await fetch(
      `${app.baseUrl}/v1/users/donor-abc/donor-presets`,
      {
        method: "PUT",
        headers: {
          "content-type": "application/json",
          authorization: `Bearer ${token}`
        },
        body: JSON.stringify({
          presets: [
            {
              restaurant_name: "Biryani Hub",
              order_url: "https://vendor.example/biryani",
              menu_items: ["Family Biriyani"],
              app_name: "Swiggy",
              source: "ai_suggestion",
              confidence: 0.81
            },
            {
              restaurant_name: "Biryani Hub",
              order_url: "https://vendor.example/biryani",
              menu_items: ["Chicken Biriyani", "Raita"],
              app_name: "Swiggy",
              source: "donor_edit",
              confidence: 0.77
            }
          ]
        })
      }
    );
    assert.equal(putResponse.status, 200);
    const putPayload = await putResponse.json();
    assert.equal(putPayload.presets.length, 1);
    assert.deepEqual(putPayload.presets[0].menu_items, ["Chicken Biriyani", "Raita"]);

    const getResponse = await fetch(
      `${app.baseUrl}/v1/users/donor-abc/donor-presets`,
      {
        headers: { authorization: `Bearer ${token}` }
      }
    );
    assert.equal(getResponse.status, 200);
    const getPayload = await getResponse.json();
    assert.equal(getPayload.presets.length, 1);
    assert.equal(getPayload.presets[0].restaurant_name, "Biryani Hub");
  } finally {
    await app.close();
  }
});

test("POST donor-presets/delete-item removes one preset", async () => {
  const app = await startServer();
  try {
    const token = issueToken("donor-del");
    const put = await fetch(`${app.baseUrl}/v1/users/donor-del/donor-presets`, {
      method: "PUT",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${token}`
      },
      body: JSON.stringify({
        presets: [
          {
            restaurant_name: "One",
            order_url: "https://one.example",
            menu_items: ["a"],
            app_name: "Z",
            source: "s",
            confidence: 0.8
          },
          {
            restaurant_name: "Two",
            order_url: "https://two.example",
            menu_items: ["b"],
            app_name: "Z",
            source: "s",
            confidence: 0.8
          }
        ]
      })
    });
    assert.equal(put.status, 200);

    const del = await fetch(
      `${app.baseUrl}/v1/users/donor-del/donor-presets/delete-item`,
      {
        method: "POST",
        headers: {
          "content-type": "application/json",
          authorization: `Bearer ${token}`
        },
        body: JSON.stringify({
          restaurant_name: "One",
          order_url: "https://one.example"
        })
      }
    );
    assert.equal(del.status, 200);
    const delBody = await del.json();
    assert.equal(delBody.presets.length, 1);
    assert.equal(delBody.presets[0].restaurant_name, "Two");

    const get = await fetch(`${app.baseUrl}/v1/users/donor-del/donor-presets`, {
      headers: { authorization: `Bearer ${token}` }
    });
    assert.equal((await get.json()).presets.length, 1);
  } finally {
    await app.close();
  }
});

test("returns 401 and 403 for auth failures", async () => {
  const app = await startServer();
  try {
    const unauthorized = await fetch(
      `${app.baseUrl}/v1/users/no-auth-user/donor-presets`
    );
    assert.equal(unauthorized.status, 401);
    const unauthorizedBody = await unauthorized.json();
    assert.equal(unauthorizedBody.code, "missing_auth_context");

    const otherToken = issueToken("other-user");
    const forbidden = await fetch(
      `${app.baseUrl}/v1/users/target-user/donor-presets`,
      {
        headers: { authorization: `Bearer ${otherToken}` }
      }
    );
    assert.equal(forbidden.status, 403);
    const forbiddenBody = await forbidden.json();
    assert.equal(forbiddenBody.code, "user_id_mismatch");
  } finally {
    await app.close();
  }
});

test("GET /health returns service identity", async () => {
  const app = await startServer();
  try {
    const res = await fetch(`${app.baseUrl}/health`);
    assert.equal(res.status, 200);
    const body = await res.json();
    assert.equal(body.ok, true);
    assert.equal(body.service, "user-service");
  } finally {
    await app.close();
  }
});

test("unknown routes return 404 JSON", async () => {
  const app = await startServer();
  try {
    const res = await fetch(`${app.baseUrl}/v1/not-a-route`);
    assert.equal(res.status, 404);
    const body = await res.json();
    assert.equal(body.code, "not_found");
  } finally {
    await app.close();
  }
});

test("POST /v1/auth/token is not available", async () => {
  const app = await startServer();
  try {
    const res = await fetch(`${app.baseUrl}/v1/auth/token`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ user_id: "demo" })
    });
    assert.equal(res.status, 404);
    const body = await res.json();
    assert.equal(body.code, "not_found");
  } finally {
    await app.close();
  }
});

test("PUT donor-presets rejects malformed JSON", async () => {
  const app = await startServer();
  try {
    const token = issueToken("json-user");
    const res = await fetch(`${app.baseUrl}/v1/users/json-user/donor-presets`, {
      method: "PUT",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${token}`
      },
      body: "not-json"
    });
    assert.equal(res.status, 400);
    const body = await res.json();
    assert.equal(body.code, "invalid_json");
  } finally {
    await app.close();
  }
});

test("PUT donor-presets requires presets array", async () => {
  const app = await startServer();
  try {
    const token = issueToken("arr-user");
    const res = await fetch(`${app.baseUrl}/v1/users/arr-user/donor-presets`, {
      method: "PUT",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${token}`
      },
      body: JSON.stringify({ presets: {} })
    });
    assert.equal(res.status, 400);
    const body = await res.json();
    assert.equal(body.code, "invalid_request");
    assert.ok(String(body.message).includes("presets must be an array"));
  } finally {
    await app.close();
  }
});

test("PUT donor-presets validates preset fields", async () => {
  const app = await startServer();
  try {
    const token = issueToken("val-user");
    const res = await fetch(`${app.baseUrl}/v1/users/val-user/donor-presets`, {
      method: "PUT",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${token}`
      },
      body: JSON.stringify({
        presets: [
          {
            restaurant_name: "",
            order_url: "https://x",
            menu_items: ["a"],
            app_name: "A",
            source: "s",
            confidence: 0
          }
        ]
      })
    });
    assert.equal(res.status, 400);
    const body = await res.json();
    assert.equal(body.code, "invalid_request");
    assert.ok(String(body.message).includes("restaurant_name"));
  } finally {
    await app.close();
  }
});

test("donor-presets path accepts URL-encoded user id", async () => {
  const app = await startServer();
  try {
    const userId = "donor/with-id";
    const token = issueToken(userId);
    const encoded = encodeURIComponent(userId);
    const put = await fetch(`${app.baseUrl}/v1/users/${encoded}/donor-presets`, {
      method: "PUT",
      headers: {
        "content-type": "application/json",
        authorization: `Bearer ${token}`
      },
      body: JSON.stringify({
        presets: [
          {
            restaurant_name: "Spot",
            order_url: "https://spot.example",
            menu_items: ["item"],
            app_name: "App",
            source: "test",
            confidence: 1
          }
        ]
      })
    });
    assert.equal(put.status, 200);

    const get = await fetch(`${app.baseUrl}/v1/users/${encoded}/donor-presets`, {
      headers: { authorization: `Bearer ${token}` }
    });
    assert.equal(get.status, 200);
    const payload = await get.json();
    assert.equal(payload.presets.length, 1);
    assert.equal(payload.presets[0].restaurant_name, "Spot");
  } finally {
    await app.close();
  }
});
