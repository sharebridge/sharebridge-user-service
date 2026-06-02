import assert from "node:assert/strict";
import test from "node:test";
import { ROLE_COORDINATOR, ROLE_DONOR } from "../src/roles.js";
import { webSignInDenialIfAny } from "../src/webSignInDenial.js";

const ENV_KEYS = ["DEPLOYMENT_ENV", "ALLOW_WEB_DASHBOARD_ANY_USER"];

function saveEnv() {
  const saved = {};
  for (const key of ENV_KEYS) {
    saved[key] = process.env[key];
  }
  return saved;
}

function restoreEnv(saved) {
  for (const key of ENV_KEYS) {
    if (saved[key] === undefined) delete process.env[key];
    else process.env[key] = saved[key];
  }
}

test("webSignInDenialIfAny allows donor when MVP active", () => {
  assert.equal(
    webSignInDenialIfAny([ROLE_DONOR], { allowWebDashboardAnyUser: true }),
    null
  );
});

test("webSignInDenialIfAny allows coordinator", () => {
  assert.equal(webSignInDenialIfAny([ROLE_COORDINATOR, ROLE_DONOR]), null);
});

test("webSignInDenialIfAny coordinator_required when MVP off", () => {
  const denial = webSignInDenialIfAny([ROLE_DONOR], {
    allowWebDashboardAnyUser: false
  });
  assert.equal(denial?.reason, "coordinator_required");
  assert.match(denial?.message ?? "", /mobile donor app/i);
});

test("webSignInDenialIfAny mvp_disabled_production when flag set on production", () => {
  const saved = saveEnv();
  process.env.DEPLOYMENT_ENV = "production";
  process.env.ALLOW_WEB_DASHBOARD_ANY_USER = "true";
  try {
    const denial = webSignInDenialIfAny([ROLE_DONOR], {
      allowWebDashboardAnyUser: false
    });
    assert.equal(denial?.reason, "mvp_disabled_production");
    assert.match(denial?.message ?? "", /DEPLOYMENT_ENV=production/i);
  } finally {
    restoreEnv(saved);
  }
});
