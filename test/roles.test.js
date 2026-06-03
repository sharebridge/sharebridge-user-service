import assert from "node:assert/strict";
import test from "node:test";
import {
  ROLE_COORDINATOR,
  ROLE_DONOR,
  clientRoleError,
  roleForClientType
} from "../src/roles.js";

test("web client allows donor or coordinator", () => {
  assert.equal(clientRoleError("web", [ROLE_DONOR]), null);
  assert.equal(clientRoleError("web", [ROLE_COORDINATOR]), null);
  assert.equal(clientRoleError("web", [ROLE_DONOR, ROLE_COORDINATOR]), null);
  assert.equal(clientRoleError("web", [])?.code, "wrong_client_role");
});

test("roleForClientType picks coordinator hat on web when present", () => {
  assert.equal(
    roleForClientType("web", [ROLE_DONOR, ROLE_COORDINATOR]),
    ROLE_COORDINATOR
  );
  assert.equal(roleForClientType("web", [ROLE_DONOR]), ROLE_DONOR);
  assert.equal(roleForClientType("android", [ROLE_DONOR]), ROLE_DONOR);
});
