import assert from "node:assert/strict";
import test from "node:test";
import {
  ENV_BYPASS_GOOGLE_SIGN_IN,
  googleSignInBypassEnabled,
  devUnlockFlagEnabled,
  isProductionDeployment
} from "../src/deploymentEnv.js";

const saved = {};

function saveEnv(keys) {
  for (const key of keys) {
    saved[key] = process.env[key];
  }
}

function restoreEnv(keys) {
  for (const key of keys) {
    if (saved[key] === undefined) delete process.env[key];
    else process.env[key] = saved[key];
  }
}

const ENV_KEYS = [
  "DEPLOYMENT_ENV",
  "NODE_ENV",
  "RENDER",
  "ALLOW_WEB_DASHBOARD_ANY_USER",
  "BYPASS_GOOGLE_SIGN_IN",
  "ALLOW_GOOGLE_SIGN_IN_BYPASS",
  "ALLOW_DEV_SIGN_IN",
  "ALLOW_DEV_TOKEN_MINT"
];

test("isProductionDeployment respects DEPLOYMENT_ENV", () => {
  saveEnv(ENV_KEYS);
  try {
    delete process.env.NODE_ENV;
    delete process.env.RENDER;
    process.env.DEPLOYMENT_ENV = "production";
    assert.equal(isProductionDeployment(), true);
    process.env.DEPLOYMENT_ENV = "staging";
    assert.equal(isProductionDeployment(), false);
    process.env.DEPLOYMENT_ENV = "development";
    assert.equal(isProductionDeployment(), false);
  } finally {
    restoreEnv(ENV_KEYS);
  }
});

test("isProductionDeployment on Render when NODE_ENV is production", () => {
  saveEnv(ENV_KEYS);
  try {
    delete process.env.DEPLOYMENT_ENV;
    process.env.NODE_ENV = "production";
    process.env.RENDER = "true";
    assert.equal(isProductionDeployment(), true);
    process.env.RENDER = "false";
    assert.equal(isProductionDeployment(), false);
  } finally {
    restoreEnv(ENV_KEYS);
  }
});

test("devUnlockFlagEnabled is false in production even when flag is true", () => {
  saveEnv(ENV_KEYS);
  try {
    process.env.DEPLOYMENT_ENV = "production";
    process.env.ALLOW_WEB_DASHBOARD_ANY_USER = "true";
    assert.equal(devUnlockFlagEnabled("ALLOW_WEB_DASHBOARD_ANY_USER"), false);
    process.env.DEPLOYMENT_ENV = "development";
    assert.equal(devUnlockFlagEnabled("ALLOW_WEB_DASHBOARD_ANY_USER"), true);
  } finally {
    restoreEnv(ENV_KEYS);
  }
});

test("googleSignInBypassEnabled accepts BYPASS_GOOGLE_SIGN_IN and legacy names", () => {
  saveEnv(ENV_KEYS);
  try {
    process.env.DEPLOYMENT_ENV = "development";
    for (const key of [
      ENV_BYPASS_GOOGLE_SIGN_IN,
      "ALLOW_GOOGLE_SIGN_IN_BYPASS",
      "ALLOW_DEV_SIGN_IN",
      "ALLOW_DEV_TOKEN_MINT"
    ]) {
      delete process.env.BYPASS_GOOGLE_SIGN_IN;
      delete process.env.ALLOW_GOOGLE_SIGN_IN_BYPASS;
      delete process.env.ALLOW_DEV_SIGN_IN;
      delete process.env.ALLOW_DEV_TOKEN_MINT;
      assert.equal(googleSignInBypassEnabled(), false);
      process.env[key] = "true";
      assert.equal(googleSignInBypassEnabled(), true);
    }
  } finally {
    restoreEnv(ENV_KEYS);
  }
});
