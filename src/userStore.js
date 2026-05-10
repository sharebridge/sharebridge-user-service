import { mkdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";

function isNonEmptyString(value) {
  return typeof value === "string" && value.trim().length > 0;
}

function keyForPreset(preset) {
  return `${preset.restaurant_name}::${preset.order_url}`;
}

export class UserStore {
  constructor({
    storagePath = path.join(process.cwd(), "data", "user-service-store.json")
  } = {}) {
    this.storagePath = storagePath;
    this.state = { users: {}, donorPresets: {} };
  }

  async init() {
    await mkdir(path.dirname(this.storagePath), { recursive: true });
    try {
      const content = await readFile(this.storagePath, "utf-8");
      const parsed = JSON.parse(content);
      this.state = {
        users: parsed.users || {},
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
      phone: isNonEmptyString(phone) ? phone.trim() : null,
      email: isNonEmptyString(email) ? email.trim() : null,
      created_at: new Date().toISOString()
    };
    this.state.users[userId] = created;
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

  async #flush() {
    await writeFile(this.storagePath, JSON.stringify(this.state, null, 2), "utf-8");
  }
}
