// Pure helpers shared by the background worker and the popup (tested with node --test).

export const HOST = "com.nexonic.downloadmanager";

export const DEFAULTS = { enabled: true, minSizeMB: 1, skipTypes: "" };

export function extensionOf(name) {
  const clean = String(name || "").split(/[?#]/)[0];
  const base = clean.split(/[\\/]/).pop();
  const dot = base.lastIndexOf(".");
  return dot > 0 ? base.slice(dot + 1).toLowerCase() : "";
}

export function parseTypes(text) {
  return String(text || "")
    .split(/[\s,]+/)
    .map((t) => t.replace(/^\./, "").toLowerCase())
    .filter(Boolean);
}

/** Should this browser download go to the app instead? */
export function shouldTakeOver(item, settings) {
  const s = { ...DEFAULTS, ...settings };
  if (!s.enabled) return false;
  const url = item.finalUrl || item.url || "";
  if (!/^https?:\/\//i.test(url)) return false; // blob:, data:, file: stay in the browser
  if (item.byExtensionId) return false;
  const size = item.totalBytes > 0 ? item.totalBytes : item.fileSize > 0 ? item.fileSize : -1;
  if (size >= 0 && size < s.minSizeMB * 1024 * 1024) return false;
  const ext = extensionOf(item.filename || url);
  if (ext && parseTypes(s.skipTypes).includes(ext)) return false;
  return true;
}

export function cookieHeader(cookies) {
  return (cookies || []).map((c) => `${c.name}=${c.value}`).join("; ");
}
