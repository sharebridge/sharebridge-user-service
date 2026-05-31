import { OAuth2Client } from "google-auth-library";

function parseClientIds() {
  const combined = [
    process.env.GOOGLE_CLIENT_ID,
    process.env.GOOGLE_CLIENT_ID_WEB,
    process.env.GOOGLE_CLIENT_ID_ANDROID,
    process.env.GOOGLE_CLIENT_ID_IOS
  ]
    .filter((value) => typeof value === "string" && value.trim())
    .flatMap((value) => value.split(","))
    .map((value) => value.trim())
    .filter(Boolean);
  return [...new Set(combined)];
}

export function createGoogleAuthVerifier({ clientIds = parseClientIds() } = {}) {
  const oauth = new OAuth2Client();
  const audiences = clientIds;

  return {
    audiences,
    async verifyAccessToken(accessToken) {
      if (typeof accessToken !== "string" || !accessToken.trim()) {
        throw new Error("access_token is required.");
      }
      const info = await oauth.getTokenInfo(accessToken.trim());
      if (!info.sub) {
        throw new Error("Google token missing subject.");
      }
      return {
        googleSub: info.sub,
        email: info.email || null,
        emailVerified: info.email_verified === true,
        name: null,
        picture: null
      };
    },
    async verifyIdToken(idToken) {
      if (typeof idToken !== "string" || !idToken.trim()) {
        throw new Error("id_token is required.");
      }
      if (audiences.length === 0) {
        throw new Error(
          "GOOGLE_CLIENT_ID (or GOOGLE_CLIENT_ID_WEB / _ANDROID) is not configured."
        );
      }
      const ticket = await oauth.verifyIdToken({
        idToken: idToken.trim(),
        audience: audiences
      });
      const payload = ticket.getPayload();
      if (!payload?.sub) {
        throw new Error("Google token missing subject.");
      }
      return {
        googleSub: payload.sub,
        email: payload.email || null,
        emailVerified: payload.email_verified === true,
        name: payload.name || null,
        picture: payload.picture || null
      };
    }
  };
}
