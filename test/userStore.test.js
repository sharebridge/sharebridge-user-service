import test from "node:test";
import assert from "node:assert/strict";
import os from "node:os";
import path from "node:path";
import { mkdir, writeFile, readFile } from "node:fs/promises";
import { UserStore } from "../src/userStore.js";

async function tempStorePath(name) {
  const dir = path.join(os.tmpdir(), `user-store-${name}-${Date.now()}-${Math.random().toString(16).slice(2)}`);
  await mkdir(dir, { recursive: true });
  return path.join(dir, "store.json");
}

test("init creates empty store file when missing", async () => {
  const storagePath = await tempStorePath("fresh");
  const store = new UserStore({ storagePath });
  await store.init();
  const raw = await readFile(storagePath, "utf-8");
  const parsed = JSON.parse(raw);
  assert.deepEqual(parsed, { users: {}, donorPresets: {} });
});

test("init loads persisted users and presets", async () => {
  const storagePath = await tempStorePath("reload");
  const payload = {
    users: {
      u1: { id: "u1", phone: null, email: null, created_at: "2020-01-01T00:00:00.000Z" }
    },
    donorPresets: {
      u1: [
        {
          id: "p1",
          restaurant_name: "Cafe",
          order_url: "https://example.com/o",
          menu_items: ["x"],
          app_name: "App",
          source: "manual",
          confidence: 1,
          saved_at: "2020-01-02T00:00:00.000Z"
        }
      ]
    }
  };
  await writeFile(storagePath, JSON.stringify(payload), "utf-8");

  const store = new UserStore({ storagePath });
  await store.init();
  assert.equal(store.state.users.u1.id, "u1");
  assert.equal(store.state.donorPresets.u1.length, 1);
  assert.equal(store.state.donorPresets.u1[0].restaurant_name, "Cafe");
});

test("getOrCreateUser rejects empty or whitespace userId", async () => {
  const store = new UserStore({ storagePath: await tempStorePath("uid") });
  await store.init();
  await assert.rejects(() => store.getOrCreateUser({ userId: "" }), /userId is required/);
  await assert.rejects(() => store.getOrCreateUser({ userId: "   " }), /userId is required/);
});

test("getOrCreateUser creates trimmed profile and merges missing phone/email", async () => {
  const store = new UserStore({ storagePath: await tempStorePath("merge") });
  await store.init();

  const first = await store.getOrCreateUser({
    userId: "alice",
    phone: "  +1  ",
    email: null
  });
  assert.equal(first.id, "alice");
  assert.equal(first.phone, "+1");

  const second = await store.getOrCreateUser({
    userId: "alice",
    phone: "+999",
    email: "  e@x.com "
  });
  assert.equal(second.phone, "+1");
  assert.equal(second.email, "e@x.com");
});

test("getOrCreateUser does not overwrite populated phone or email", async () => {
  const store = new UserStore({ storagePath: await tempStorePath("no-overwrite") });
  await store.init();
  await store.getOrCreateUser({
    userId: "bob",
    phone: "+111",
    email: "bob@example.com"
  });
  const again = await store.getOrCreateUser({
    userId: "bob",
    phone: "+222",
    email: "other@example.com"
  });
  assert.equal(again.phone, "+111");
  assert.equal(again.email, "bob@example.com");
});

test("listDonorPresets returns empty array for missing user or invalid userId", async () => {
  const store = new UserStore({ storagePath: await tempStorePath("list") });
  await store.init();
  await store.replaceDonorPresets("norm", [
    {
      id: "a",
      restaurant_name: "R",
      order_url: "u",
      menu_items: ["m"],
      app_name: "A",
      source: "s",
      confidence: 0,
      saved_at: "2020-01-01T00:00:00.000Z"
    }
  ]);

  assert.deepEqual(await store.listDonorPresets("nobody"), []);
  assert.deepEqual(await store.listDonorPresets(""), []);
  assert.deepEqual(await store.listDonorPresets("   "), []);
  assert.equal((await store.listDonorPresets("norm")).length, 1);
});

test("replaceDonorPresets rejects empty userId", async () => {
  const store = new UserStore({ storagePath: await tempStorePath("put") });
  await store.init();
  await assert.rejects(
    () =>
      store.replaceDonorPresets(" ", [
        {
          restaurant_name: "R",
          order_url: "u",
          menu_items: [],
          app_name: "A",
          source: "s",
          confidence: 0
        }
      ]),
    /userId is required/
  );
});

test("replaceDonorPresets dedupes by restaurant_name and order_url (latest wins)", async () => {
  const store = new UserStore({ storagePath: await tempStorePath("dedupe") });
  await store.init();
  const out = await store.replaceDonorPresets("uid", [
    {
      restaurant_name: "Same",
      order_url: "https://same",
      menu_items: ["old"],
      app_name: "App",
      source: "first",
      confidence: 0.1,
      saved_at: "2020-01-01T00:00:00.000Z"
    },
    {
      restaurant_name: "Same",
      order_url: "https://same",
      menu_items: ["new"],
      app_name: "App",
      source: "second",
      confidence: 0.9,
      saved_at: "2020-01-02T00:00:00.000Z"
    }
  ]);
  assert.equal(out.length, 1);
  assert.deepEqual(out[0].menu_items, ["new"]);
  assert.equal(out[0].source, "second");
});

test("replaceDonorPresets fills id and saved_at when omitted", async () => {
  const store = new UserStore({ storagePath: await tempStorePath("defaults") });
  await store.init();
  const [preset] = await store.replaceDonorPresets("u", [
    {
      restaurant_name: "Z",
      order_url: "https://z",
      menu_items: ["a"],
      app_name: "App",
      source: "s",
      confidence: 0
    }
  ]);
  assert.ok(typeof preset.id === "string" && preset.id.includes("u-preset"));
  assert.ok(/^\d{4}-/.test(preset.saved_at));
});

test("state survives new UserStore on same storagePath", async () => {
  const storagePath = await tempStorePath("persist");
  const a = new UserStore({ storagePath });
  await a.init();
  await a.getOrCreateUser({ userId: "persist-user" });
  await a.replaceDonorPresets("persist-user", [
    {
      restaurant_name: "X",
      order_url: "y",
      menu_items: ["z"],
      app_name: "A",
      source: "s",
      confidence: 0
    }
  ]);

  const b = new UserStore({ storagePath });
  await b.init();
  assert.ok(b.state.users["persist-user"]);
  assert.equal((await b.listDonorPresets("persist-user")).length, 1);
});
