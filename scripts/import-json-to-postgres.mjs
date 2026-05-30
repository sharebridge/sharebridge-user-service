#!/usr/bin/env node
/**
 * One-time import: data/user-service-store.json → PostgreSQL.
 * Usage: DATABASE_URL=... node scripts/import-json-to-postgres.mjs
 */
import "dotenv/config";
import { readFile } from "node:fs/promises";
import path from "node:path";
import pg from "pg";
import { fileURLToPath } from "node:url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(__dirname, "..");

async function main() {
  const databaseUrl = process.env.DATABASE_URL?.trim();
  if (!databaseUrl) {
    console.error("DATABASE_URL is required.");
    process.exit(1);
  }
  const pool = new pg.Pool({ connectionString: databaseUrl });
  const storePath = path.join(root, "data", "user-service-store.json");
  let state = { users: {}, usersByGoogleSub: {}, donorPresets: {} };
  try {
    state = JSON.parse(await readFile(storePath, "utf-8"));
  } catch (error) {
    if (error.code !== "ENOENT") {
      throw error;
    }
    console.warn("No user-service-store.json — skipping user/preset import.");
  }

  for (const user of Object.values(state.users || {})) {
    if (!user?.id) {
      continue;
    }
    await pool.query(
      `INSERT INTO users (id, google_sub, email, name, picture, phone, created_at, updated_at)
       VALUES ($1, $2, $3, $4, $5, $6, COALESCE($7::timestamptz, now()), now())
       ON CONFLICT (id) DO UPDATE SET
         google_sub = COALESCE(EXCLUDED.google_sub, users.google_sub),
         email = COALESCE(EXCLUDED.email, users.email),
         name = COALESCE(EXCLUDED.name, users.name),
         picture = COALESCE(EXCLUDED.picture, users.picture),
         phone = COALESCE(EXCLUDED.phone, users.phone),
         updated_at = now()`,
      [
        user.id,
        user.google_sub ?? null,
        user.email ?? null,
        user.name ?? null,
        user.picture ?? null,
        user.phone ?? null,
        user.created_at ?? null
      ]
    );
    await pool.query(
      "INSERT INTO user_roles (user_id, role) VALUES ($1, 'donor') ON CONFLICT DO NOTHING",
      [user.id]
    );
    if (user.role === "coordinator") {
      await pool.query(
        "INSERT INTO user_roles (user_id, role) VALUES ($1, 'coordinator') ON CONFLICT DO NOTHING",
        [user.id]
      );
    }
    const presets = state.donorPresets?.[user.id];
    if (Array.isArray(presets) && presets.length > 0) {
      await pool.query(
        `INSERT INTO donor_presets (user_id, presets_json, updated_at)
         VALUES ($1, $2::jsonb, now())
         ON CONFLICT (user_id) DO UPDATE SET presets_json = EXCLUDED.presets_json, updated_at = now()`,
        [user.id, JSON.stringify(presets)]
      );
    }
  }

  await pool.end();
  console.log("Import complete.");
}

main().catch((error) => {
  console.error(error);
  process.exit(1);
});
