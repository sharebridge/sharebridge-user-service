import crypto from "node:crypto";

function envStr(name, fallback) {
  const v = process.env[name];
  if (typeof v !== "string" || v.trim() === "") return fallback;
  return v.trim();
}

/** Read at mint/verify time so tests can set env without module-order issues */
function defaults() {
  return {
    issuer: envStr("AUTH_TOKEN_ISSUER", "sharingbridge-user-service"),
    audience: envStr("AUTH_TOKEN_AUDIENCE", "sharingbridge-clients"),
    ttlSeconds: Number(process.env.AUTH_TOKEN_TTL_SECONDS || 3600),
    secret: envStr(
      "AUTH_TOKEN_SECRET",
      "sharingbridge-dev-secret-change-me"
    )
  };
}

function base64UrlEncodeJson(value) {
  return Buffer.from(JSON.stringify(value)).toString("base64url");
}

function sign(data, secret) {
  return crypto.createHmac("sha256", secret).update(data).digest("base64url");
}

export function mintAuthToken(userId, options = {}) {
  const now = Math.floor(Date.now() / 1000);
  const d = defaults();
  const issuer = options.issuer ?? d.issuer;
  const audience = options.audience ?? d.audience;
  const ttlSeconds =
    typeof options.ttlSeconds === "number" ? options.ttlSeconds : d.ttlSeconds;
  const secret = options.secret ?? d.secret;
  const role =
    typeof options.role === "string" && options.role.trim()
      ? options.role.trim()
      : "initiator";
  const roles = Array.isArray(options.roles)
    ? options.roles.filter((r) => typeof r === "string" && r.trim())
    : [role];
  const payload = {
    sub: userId,
    role,
    roles,
    iss: issuer,
    aud: audience,
    iat: now,
    exp: now + ttlSeconds
  };
  const header = { alg: "HS256", typ: "JWT" };
  const encodedHeader = base64UrlEncodeJson(header);
  const encodedPayload = base64UrlEncodeJson(payload);
  const signature = sign(`${encodedHeader}.${encodedPayload}`, secret);
  return `${encodedHeader}.${encodedPayload}.${signature}`;
}

export function verifyAuthToken(token, options = {}) {
  if (typeof token !== "string" || !token.trim()) {
    throw new Error("Token is required.");
  }
  const d = defaults();
  const secret = options.secret ?? d.secret;
  const issuer = options.issuer ?? d.issuer;
  const audience = options.audience ?? d.audience;

  const parts = token.split(".");
  if (parts.length !== 3) {
    throw new Error("Token format is invalid.");
  }
  const [encodedHeader, encodedPayload, encodedSignature] = parts;
  const expectedSignature = sign(`${encodedHeader}.${encodedPayload}`, secret);
  const givenSig = Buffer.from(encodedSignature);
  const expectedSig = Buffer.from(expectedSignature);
  if (
    givenSig.length !== expectedSig.length ||
    !crypto.timingSafeEqual(givenSig, expectedSig)
  ) {
    throw new Error("Token signature is invalid.");
  }

  const payload = JSON.parse(
    Buffer.from(encodedPayload, "base64url").toString("utf-8")
  );
  const now = Math.floor(Date.now() / 1000);
  if (payload.iss !== issuer) {
    throw new Error("Token issuer is invalid.");
  }
  if (payload.aud !== audience) {
    throw new Error("Token audience is invalid.");
  }
  if (typeof payload.exp !== "number" || payload.exp <= now) {
    throw new Error("Token is expired.");
  }
  if (typeof payload.sub !== "string" || !payload.sub.trim()) {
    throw new Error("Token subject is invalid.");
  }
  return payload;
}
