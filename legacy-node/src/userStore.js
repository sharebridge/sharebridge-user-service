import { createHash } from "node:crypto";
import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { ROLE_COORDINATOR, ROLE_DONOR } from "./roles.js";

function isNonEmptyString(value) {
  return typeof value === "string" && value.trim().length > 0;
}

function keyFromPair(restaurantName, orderUrl) {
  const r = String(restaurantName ?? "").trim();
  const u = String(orderUrl ?? "").trim();
  return `${r}::${u}`;
}

function keyForPreset(preset) {
  return keyFromPair(preset.restaurant_name, preset.order_url);
}

export class UserStore {
  constructor({
    storagePath = path.join(process.cwd(), "data", "user-service-store.json")
  } = {}) {
    this.storagePath = storagePath;
    this.state = { users: {}, usersByGoogleSub: {}, donorPresets: {} };
  }

  async getRolesForUser(userId) {
    const user = this.state.users[userId];
    if (!user) {
      return [ROLE_DONOR];
    }
    const roles = new Set([ROLE_DONOR]);
    if (user.role === ROLE_COORDINATOR) {
      roles.add(ROLE_COORDINATOR);
    }
    return [...roles];
  }

  async ensureRole(userId, role) {
    const user = await this.getOrCreateUser({ userId });
    if (role === ROLE_COORDINATOR && user.role !== ROLE_COORDINATOR) {
      user.role = ROLE_COORDINATOR;
      await this.#flush();
    }
  }

  async init() {
    await mkdir(path.dirname(this.storagePath), { recursive: true });
    try {
      const content = await readFile(this.storagePath, "utf-8");
      const parsed = JSON.parse(content);
      this.state = {
        users: parsed.users || {},
        usersByGoogleSub: parsed.usersByGoogleSub || {},
        donorPresets: parsed.donorPresets || {}
      };
    } catch (error) {
      if (error.code !== "ENOENT") {
        throw error;
      }
      await this.#flush();
    }
  }

  async getOrCreateUser({ userId, phone = null, email = null }) {
    if (!isNonEmptyString(userId)) {
      throw new Error("userId is required.");
    }
    const existing = this.state.users[userId];
    if (existing) {
      let changed = false;
      if (!existing.user_id) {
        existing.user_id = userId;
        changed = true;
      }
      if (!existing.role) {
        existing.role = ROLE_DONOR;
        changed = true;
      }
      if (!existing.phone && isNonEmptyString(phone)) {
        existing.phone = phone.trim();
        changed = true;
      }
      if (!existing.email && isNonEmptyString(email)) {
        existing.email = email.trim();
        changed = true;
      }
      if (changed) {
        await this.#flush();
      }
      return existing;
    }

    const created = {
      id: userId,
      user_id: userId,
      phone: isNonEmptyString(phone) ? phone.trim() : null,
      email: isNonEmptyString(email) ? email.trim() : null,
      role: ROLE_DONOR,
      google_sub: null,
      name: null,
      picture: null,
      created_at: new Date().toISOString()
    };
    this.state.users[userId] = created;
    await this.#flush();
    return created;
  }

  #userIdFromGoogleSub(googleSub) {
    const digest = createHash("sha256").update(googleSub).digest("hex").slice(0, 16);
    return `u_${digest}`;
  }

  async findOrCreateGoogleUser({ googleSub, email, name, picture, role }) {
    if (!isNonEmptyString(googleSub)) {
      throw new Error("googleSub is required.");
    }
    const existingId = this.state.usersByGoogleSub[googleSub.trim()];
    if (existingId && this.state.users[existingId]) {
      const user = this.state.users[existingId];
      let changed = false;
      if (isNonEmptyString(email) && user.email !== email.trim()) {
        user.email = email.trim();
        changed = true;
      }
      if (isNonEmptyString(name) && user.name !== name.trim()) {
        user.name = name.trim();
        changed = true;
      }
      if (isNonEmptyString(picture) && user.picture !== picture.trim()) {
        user.picture = picture.trim();
        changed = true;
      }
      if (typeof role === "string" && role && user.role !== role) {
        user.role = role;
        changed = true;
      }
      if (changed) {
        await this.#flush();
      }
      return user;
    }

    const userId = this.#userIdFromGoogleSub(googleSub);
    const created = {
      id: userId,
      user_id: userId,
      phone: null,
      email: isNonEmptyString(email) ? email.trim() : null,
      role: typeof role === "string" && role ? role : ROLE_DONOR,
      google_sub: googleSub.trim(),
      name: isNonEmptyString(name) ? name.trim() : null,
      picture: isNonEmptyString(picture) ? picture.trim() : null,
      created_at: new Date().toISOString()
    };
    this.state.users[userId] = created;
    this.state.usersByGoogleSub[googleSub.trim()] = userId;
    await this.#flush();
    return created;
  }

  async listDonorPresets(userId) {
    if (!isNonEmptyString(userId)) {
      return [];
    }
    return this.state.donorPresets[userId] || [];
  }

  async replaceDonorPresets(userId, presets) {
    if (!isNonEmptyString(userId)) {
      throw new Error("userId is required.");
    }

    const now = new Date().toISOString();
    const deduped = new Map();
    for (const preset of presets) {
      const normalized = {
        id:
          isNonEmptyString(preset.id) && preset.id.trim().length > 0
            ? preset.id
            : `${userId}-preset-${Date.now()}-${Math.random()
                .toString(16)
                .slice(2, 8)}`,
        restaurant_name: preset.restaurant_name,
        order_url: preset.order_url,
        menu_items: preset.menu_items,
        app_name: preset.app_name,
        source: preset.source,
        confidence: preset.confidence,
        saved_at:
          isNonEmptyString(preset.saved_at) && preset.saved_at.trim().length > 0
            ? preset.saved_at
            : now
      };
      deduped.set(keyForPreset(normalized), normalized);
    }

    const updated = [...deduped.values()];
    this.state.donorPresets[userId] = updated;
    await this.#flush();
    return updated;
  }

  /**
   * Removes one preset matching trimmed (restaurant_name, order_url), same key as dedupe.
   */
  async deleteDonorPreset(userId, { restaurant_name, order_url }) {
    if (!isNonEmptyString(userId)) {
      throw new Error("userId is required.");
    }
    const target = keyFromPair(restaurant_name, order_url);
    const list = this.state.donorPresets[userId] || [];
    const next = list.filter((p) => keyForPreset(p) !== target);
    this.state.donorPresets[userId] = next;
    await this.#flush();
    return next;
  }

  async #flush() {
    await writeFile(this.storagePath, JSON.stringify(this.state, null, 2), "utf-8");
  }
}
