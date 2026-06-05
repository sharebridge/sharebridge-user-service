import test from "node:test";
import assert from "node:assert/strict";
import { logStartupFromIssues, shouldLog } from "../src/serviceLog.js";
import {
  buildStartupConfig,
  collectStartupIssues
} from "../src/startupConfig.js";

test("shouldLog defaults to warn", () => {
  assert.equal(shouldLog("warn", {}), true);
  assert.equal(shouldLog("info", {}), false);
});

test("collectStartupIssues flags missing Google web client", () => {
  const config = buildStartupConfig({
    DATABASE_URL: "postgresql://x",
    WEB_CORS_ORIGINS: "http://localhost:5173"
  });
  const issues = collectStartupIssues(config);
  assert.ok(issues.includes("GOOGLE_CLIENT_ID_WEB is unset"));
});

test("logStartupFromIssues warns but skips full config at warn level", () => {
  const warnings = [];
  const config = buildStartupConfig({});
  logStartupFromIssues(
    config,
    collectStartupIssues(config),
    { warn: (line) => warnings.push(line) },
    { LOG_LEVEL: "warn" }
  );
  assert.ok(warnings.some((line) => line.includes("config issues")));
});
