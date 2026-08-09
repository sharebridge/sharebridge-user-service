#!/usr/bin/env node
/**
 * One-off: grant coordinator role by email.
 * Usage: DATABASE_URL=... node scripts/seed-coordinator-by-email.mjs you@gmail.com
 */
import "dotenv/config";
import pg from "pg";

const email = process.argv[2]?.trim();
if (!email) {
  console.error("Usage: node scripts/seed-coordinator-by-email.mjs <email>");
  process.exit(1);
}

const databaseUrl = process.env.DATABASE_URL?.trim();
if (!databaseUrl) {
  console.error("DATABASE_URL is required in .env");
  process.exit(1);
}

const pool = new pg.Pool({ connectionString: databaseUrl });
try {
  const user = await pool.query(
    "SELECT id, email FROM users WHERE lower(email) = lower($1)",
    [email]
  );
  if (user.rowCount === 0) {
    console.error(
      `No user row for ${email}. Sign in once on mobile or web first, then re-run.`
    );
    process.exit(1);
  }
  await pool.query(
    "INSERT INTO user_roles (user_id, role) VALUES ($1, 'coordinator') ON CONFLICT DO NOTHING",
    [user.rows[0].id]
  );
  await pool.query(
    "INSERT INTO user_roles (user_id, role) VALUES ($1, 'donor') ON CONFLICT DO NOTHING",
    [user.rows[0].id]
  );
  const roles = await pool.query(
    `SELECT role FROM user_roles WHERE user_id = $1 ORDER BY role`,
    [user.rows[0].id]
  );
  console.log(`OK: ${email} (${user.rows[0].id})`);
  console.log("roles:", roles.rows.map((r) => r.role).join(", "));
} finally {
  await pool.end();
}
