import { DEFAULTS } from "./helpers.js";

const $ = (id) => document.getElementById(id);

const s = { ...DEFAULTS, ...(await chrome.storage.sync.get(Object.keys(DEFAULTS))) };
$("enabled").checked = s.enabled;
$("min").value = s.minSizeMB;
$("skip").value = s.skipTypes;

$("enabled").onchange = () => chrome.storage.sync.set({ enabled: $("enabled").checked });
$("min").onchange = () => chrome.storage.sync.set({ minSizeMB: Math.max(0, Number($("min").value) || 0) });
$("skip").onchange = () => chrome.storage.sync.set({ skipTypes: $("skip").value });

$("send").onclick = async () => {
  const url = $("link").value.trim();
  if (!/^https?:\/\//i.test(url)) return;
  const r = await chrome.runtime.sendMessage({ type: "send-link", url });
  $("link").value = r && r.ok ? "" : url;
};

const r = await chrome.runtime.sendMessage({ type: "ping" });
$("status").textContent = r && r.ok ? `Connected to the app (${r.version})` : "The app is not reachable. Open it once after installing.";
$("status").className = r && r.ok ? "ok" : "bad";
