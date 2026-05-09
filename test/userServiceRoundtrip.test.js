import test from "node:test";
import assert from "node:assert/strict";
import os from "node:os";
import path from "node:path";
import { mkdir } from "node:fs/promises";
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

test("issues demo token and creates donor user model", async () => {
  const app = await startServer();
  try {
    const response = await fetch(`${app.baseUrl}/v1/auth/demo-token`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({
        user_id: "donor-001",
        phone: "+911234567890",
        email: "donor@example.com"
      })
    });
    assert.equal(response.status, 200);
    const payload = await response.json();
    assert.equal(payload.token, "demo.donor-001");
    assert.equal(payload.user.id, "donor-001");
    assert.equal(payload.user.phone, "+911234567890");
    assert.equal(payload.user.email, "donor@example.com");
    assert.ok(payload.user.created_at);
  } finally {
    await app.close();
  }
});

test("upserts donor presets and returns deduped set", async () => {
  const app = await startServer();
  try {
    const putResponse = await fetch(
      `${app.baseUrl}/v1/users/donor-abc/donor-presets`,
      {
        method: "PUT",
        headers: {
          "content-type": "application/json",
          authorization: "Bearer demo.donor-abc"
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
        headers: { authorization: "Bearer demo.donor-abc" }
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

test("returns 401 and 403 for auth failures", async () => {
  const app = await startServer();
  try {
    const unauthorized = await fetch(
      `${app.baseUrl}/v1/users/no-auth-user/donor-presets`
    );
    assert.equal(unauthorized.status, 401);
    const unauthorizedBody = await unauthorized.json();
    assert.equal(unauthorizedBody.code, "missing_auth_context");

    const forbidden = await fetch(
      `${app.baseUrl}/v1/users/target-user/donor-presets`,
      {
        headers: { authorization: "Bearer demo.other-user" }
      }
    );
    assert.equal(forbidden.status, 403);
    const forbiddenBody = await forbidden.json();
    assert.equal(forbiddenBody.code, "user_id_mismatch");
  } finally {
    await app.close();
  }
});
