#!/usr/bin/env node
/**
 * Sign a dev JWT with AUTH_TOKEN_SECRET (same as integration-service / mobile --dart-define=AUTH_TOKEN).
 * Usage: node scripts/mint-dev-jwt.mjs <user_id> [role]
 * Example: node scripts/mint-dev-jwt.mjs demo-user initiator
 */
import "dotenv/config";
import { mintAuthToken } from "../src/tokenService.js";

const userId = process.argv[2]?.trim();
const roleArg = process.argv[3]?.trim() || "initiator";
const role = roleArg === "donor" ? "initiator" : roleArg;
if (!userId) {
  console.error(
    "Usage: node scripts/mint-dev-jwt.mjs <user_id> [initiator|coordinator]"
  );
  process.exit(1);
}
console.log(mintAuthToken(userId, { role }));
