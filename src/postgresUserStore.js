import { createHash } from "node:crypto";
import pg from "pg";
import {
  keyForPreset,
  keyFromPair,
  normalizePresetsForStorage
} from "./donorPresetUtils.js";
import { ROLE_DONOR } from "./roles.js";

function isNonEmptyString(value) {
  return typeof value === "string" && value.trim().length > 0;
}

function userIdFromGoogleSub(googleSub) {
  const digest = createHash("sha256").update(googleSub).digest("hex").slice(0, 16);
  return `u_${digest}`;
}

function rowToUser(row, activeRole = ROLE_DONOR) {
  const createdAt = row.created_at;
  return {
    id: row.id,
    user_id: row.id,
    phone: row.phone,
    email: row.email,
    role: activeRole,
    google_sub: row.google_sub,
    name: row.name,
    picture: row.picture,
    created_at:
      createdAt instanceof Date ? createdAt.toISOString() : String(createdAt ?? "")
  };
}

export class PostgresUserStore {
  constructor(pool) {
    this.pool = pool;
  }

  static async create(connectionString) {
    if (!isNonEmptyString(connectionString)) {
      throw new Error("DATABASE_URL is required for PostgresUserStore.");
    }
    const pool = new pg.Pool({ connectionString: connectionString.trim() });
    const client = await pool.connect();
    try {
      await client.query("SELECT 1");
    } finally {
      client.release();
    }
    return new PostgresUserStore(pool);
  }

  async init() {}

  async getRolesForUser(userId) {
    const result = await this.pool.query(
      "SELECT role FROM user_roles WHERE user_id = $1 ORDER BY role",
      [userId]
    );
    return result.rows.map((row) => row.role);
  }

  async ensureRole(userId, role) {
    await this.pool.query(
      "INSERT INTO user_roles (user_id, role) VALUES ($1, $2) ON CONFLICT DO NOTHING",
      [userId, role]
    );
  }

  async isCoordinatorByEmail(email) {
    const normalized = typeof email === "string" ? email.trim().toLowerCase() : "";
    if (!normalized) {
      return false;
    }
    const result = await this.pool.query(
      `SELECT 1
       FROM users u
       INNER JOIN user_roles ur ON ur.user_id = u.id AND ur.role = 'coordinator'
       WHERE lower(u.email) = $1
       LIMIT 1`,
      [normalized]
    );
    return result.rowCount > 0;
  }

  async getOrCreateUser({ userId, phone = null, email = null }) {
    if (!isNonEmptyString(userId)) {
      throw new Error("userId is required.");
    }
    const existing = await this.pool.query("SELECT * FROM users WHERE id = $1", [userId]);
    if (existing.rowCount > 0) {
      const row = existing.rows[0];
      const updated = await this.pool.query(
        `UPDATE users SET
           phone = COALESCE($2, phone),
           email = COALESCE($3, email),
           updated_at = now()
         WHERE id = $1
         RETURNING *`,
        [
          userId,
          isNonEmptyString(phone) ? phone.trim() : null,
          isNonEmptyString(email) ? email.trim() : null
        ]
      );
      await this.ensureRole(userId, ROLE_DONOR);
      const roles = await this.getRolesForUser(userId);
      const activeRole = roles.includes("coordinator") ? "coordinator" : ROLE_DONOR;
      return rowToUser(updated.rows[0], activeRole);
    }

    const inserted = await this.pool.query(
      `INSERT INTO users (id, phone, email, google_sub, name, picture)
       VALUES ($1, $2, $3, NULL, NULL, NULL)
       RETURNING *`,
      [
        userId,
        isNonEmptyString(phone) ? phone.trim() : null,
        isNonEmptyString(email) ? email.trim() : null
      ]
    );
    await this.ensureRole(userId, ROLE_DONOR);
    return rowToUser(inserted.rows[0], ROLE_DONOR);
  }

  async findOrCreateGoogleUser({ googleSub, email, name, picture }) {
    if (!isNonEmptyString(googleSub)) {
      throw new Error("googleSub is required.");
    }
    const sub = googleSub.trim();
    const bySub = await this.pool.query("SELECT * FROM users WHERE google_sub = $1", [sub]);
    if (bySub.rowCount > 0) {
      const updated = await this.pool.query(
        `UPDATE users SET
           email = COALESCE($2, email),
           name = COALESCE($3, name),
           picture = COALESCE($4, picture),
           updated_at = now()
         WHERE google_sub = $1
         RETURNING *`,
        [
          sub,
          isNonEmptyString(email) ? email.trim() : null,
          isNonEmptyString(name) ? name.trim() : null,
          isNonEmptyString(picture) ? picture.trim() : null
        ]
      );
      const userId = updated.rows[0].id;
      await this.ensureRole(userId, ROLE_DONOR);
      const roles = await this.getRolesForUser(userId);
      const activeRole = roles.includes("coordinator") ? "coordinator" : ROLE_DONOR;
      return rowToUser(updated.rows[0], activeRole);
    }

    const userId = userIdFromGoogleSub(sub);
    const inserted = await this.pool.query(
      `INSERT INTO users (id, google_sub, email, name, picture, phone)
       VALUES ($1, $2, $3, $4, $5, NULL)
       RETURNING *`,
      [
        userId,
        sub,
        isNonEmptyString(email) ? email.trim() : null,
        isNonEmptyString(name) ? name.trim() : null,
        isNonEmptyString(picture) ? picture.trim() : null
      ]
    );
    await this.ensureRole(userId, ROLE_DONOR);
    return rowToUser(inserted.rows[0], ROLE_DONOR);
  }

  async listDonorPresets(userId) {
    if (!isNonEmptyString(userId)) {
      return [];
    }
    const result = await this.pool.query(
      "SELECT presets_json FROM donor_presets WHERE user_id = $1",
      [userId]
    );
    if (result.rowCount === 0) {
      return [];
    }
    const json = result.rows[0].presets_json;
    return Array.isArray(json) ? json : [];
  }

  async replaceDonorPresets(userId, presets) {
    if (!isNonEmptyString(userId)) {
      throw new Error("userId is required.");
    }
    const updated = normalizePresetsForStorage(userId, presets);
    await this.pool.query(
      `INSERT INTO donor_presets (user_id, presets_json, updated_at)
       VALUES ($1, $2::jsonb, now())
       ON CONFLICT (user_id) DO UPDATE SET
         presets_json = EXCLUDED.presets_json,
         updated_at = now()`,
      [userId, JSON.stringify(updated)]
    );
    return updated;
  }

  async deleteDonorPreset(userId, { restaurant_name, order_url }) {
    if (!isNonEmptyString(userId)) {
      throw new Error("userId is required.");
    }
    const list = await this.listDonorPresets(userId);
    const target = keyFromPair(restaurant_name, order_url);
    const next = list.filter((p) => keyForPreset(p) !== target);
    await this.replaceDonorPresets(userId, next);
    return next;
  }
}
