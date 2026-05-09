import crypto from "node:crypto";

const DEFAULT_ISSUER = process.env.AUTH_TOKEN_ISSUER || "sharebridge-user-service";
const DEFAULT_AUDIENCE = process.env.AUTH_TOKEN_AUDIENCE || "sharebridge-clients";
const DEFAULT_TTL_SECONDS = Number(process.env.AUTH_TOKEN_TTL_SECONDS || 3600);
const DEFAULT_SECRET =
  process.env.AUTH_TOKEN_SECRET || "sharebridge-dev-secret-change-me";

function base64UrlEncodeJson(value) {
  return Buffer.from(JSON.stringify(value)).toString("base64url");
}

function sign(data, secret) {
  return crypto.createHmac("sha256", secret).update(data).digest("base64url");
}

export function mintAuthToken(userId, options = {}) {
  const now = Math.floor(Date.now() / 1000);
  const issuer = options.issuer || DEFAULT_ISSUER;
  const audience = options.audience || DEFAULT_AUDIENCE;
  const ttlSeconds = options.ttlSeconds || DEFAULT_TTL_SECONDS;
  const secret = options.secret || DEFAULT_SECRET;
  const payload = {
    sub: userId,
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
  const secret = options.secret || DEFAULT_SECRET;
  const issuer = options.issuer || DEFAULT_ISSUER;
  const audience = options.audience || DEFAULT_AUDIENCE;

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
