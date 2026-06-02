import assert from "node:assert/strict";
import test from "node:test";
import { ROLE_COORDINATOR, ROLE_DONOR, clientRoleError } from "../src/roles.js";

test("web client requires coordinator by default", () => {
  assert.equal(
    clientRoleError("web", [ROLE_DONOR])?.code,
    "wrong_client_role"
  );
  assert.equal(clientRoleError("web", [ROLE_COORDINATOR]), null);
});

test("web client allows donor when allowWebDashboardAnyUser is set", () => {
  assert.equal(
    clientRoleError("web", [ROLE_DONOR], { allowWebDashboardAnyUser: true }),
    null
  );
  assert.equal(
    clientRoleError("web", [], { allowWebDashboardAnyUser: true })?.code,
    "wrong_client_role"
  );
});
