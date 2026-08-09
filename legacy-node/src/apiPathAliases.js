/**
 * @param {string | undefined} url
 * @returns {string | undefined}
 */
export function normalizeUserServiceApiPath(url) {
  if (typeof url !== "string" || !url) {
    return url;
  }
  const qIndex = url.indexOf("?");
  const path = qIndex >= 0 ? url.slice(0, qIndex) : url;
  const query = qIndex >= 0 ? url.slice(qIndex) : "";
  if (path.includes("/initiator-presets")) {
    return `${path.replaceAll("/initiator-presets", "/donor-presets")}${query}`;
  }
  return url;
}
