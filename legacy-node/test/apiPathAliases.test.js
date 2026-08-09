import assert from "node:assert/strict";
import test from "node:test";
import { normalizeUserServiceApiPath } from "../src/apiPathAliases.js";

test("normalizeUserServiceApiPath maps initiator-presets to donor-presets", () => {
  assert.equal(
    normalizeUserServiceApiPath("/v1/users/alice/initiator-presets"),
    "/v1/users/alice/donor-presets"
  );
  assert.equal(
    normalizeUserServiceApiPath(
      "/v1/users/alice/initiator-presets/delete-item"
    ),
    "/v1/users/alice/donor-presets/delete-item"
  );
});
