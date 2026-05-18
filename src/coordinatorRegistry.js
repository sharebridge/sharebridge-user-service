import { readFile } from "node:fs/promises";
import path from "node:path";

function normalizeEmail(email) {
  return typeof email === "string" ? email.trim().toLowerCase() : "";
}

/**
 * Local allowlist for coordinator role (emails).
 * File: data/coordinators.json → { "emails": ["coord@example.com"] }
 * Env: COORDINATOR_EMAILS=comma,separated,list (merged with file)
 */
export class CoordinatorRegistry {
  constructor({
    filePath = path.join(process.cwd(), "data", "coordinators.json"),
    envEmails = process.env.COORDINATOR_EMAILS
  } = {}) {
    this.filePath = filePath;
    this.envEmails = envEmails;
    this.emails = new Set();
  }

  async init() {
    const fromFile = await this.#loadFile();
    const fromEnv = this.#parseEnv(this.envEmails);
    this.emails = new Set([...fromFile, ...fromEnv].filter(Boolean));
  }

  isCoordinator(email) {
    const normalized = normalizeEmail(email);
    return normalized.length > 0 && this.emails.has(normalized);
  }

  async #loadFile() {
    try {
      const raw = await readFile(this.filePath, "utf-8");
      const parsed = JSON.parse(raw);
      if (!Array.isArray(parsed?.emails)) {
        return [];
      }
      return parsed.emails.map(normalizeEmail).filter(Boolean);
    } catch (error) {
      if (error?.code === "ENOENT") {
        return [];
      }
      throw error;
    }
  }

  #parseEnv(value) {
    if (typeof value !== "string" || !value.trim()) {
      return [];
    }
    return value
      .split(",")
      .map(normalizeEmail)
      .filter(Boolean);
  }
}
