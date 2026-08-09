import assert from "node:assert/strict";
import test from "node:test";
import {
  ROLE_COORDINATOR,
  ROLE_DONOR,
  ROLE_INITIATOR,
  clientRoleError,
  roleForClientType
} from "../src/roles.js";

test("web client allows initiator, legacy donor, or coordinator", () => {
  assert.equal(clientRoleError("web", [ROLE_INITIATOR]), null);
  assert.equal(clientRoleError("web", [ROLE_DONOR]), null);
  assert.equal(clientRoleError("web", [ROLE_COORDINATOR]), null);
  assert.equal(
    clientRoleError("web", [ROLE_DONOR, ROLE_COORDINATOR]),
    null
  );
  assert.equal(clientRoleError("web", [])?.code, "wrong_client_role");
});

test("mobile client accepts legacy donor role in roles array", () => {
  assert.equal(clientRoleError("android", [ROLE_DONOR]), null);
  assert.equal(clientRoleError("android", [ROLE_INITIATOR]), null);
  assert.equal(clientRoleError("android", [])?.reason, "no_initiator_role");
});

test("roleForClientType picks coordinator hat on web when present", () => {
  assert.equal(
    roleForClientType("web", [ROLE_DONOR, ROLE_COORDINATOR]),
    ROLE_COORDINATOR
  );
  assert.equal(roleForClientType("web", [ROLE_DONOR]), ROLE_INITIATOR);
  assert.equal(roleForClientType("android", [ROLE_DONOR]), ROLE_INITIATOR);
});
