import { DEFAULTS, filterLinks, isVideoSite, qualityLabel } from "./helpers.js";

const $ = (id) => document.getElementById(id);
const ask = (msg) => chrome.runtime.sendMessage(msg);
const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });

// Settings
const s = { ...DEFAULTS, ...(await chrome.storage.sync.get(Object.keys(DEFAULTS))) };
$("enabled").checked = s.enabled;
$("video").checked = s.videoButton;
$("min").value = s.minSizeMB;
$("skip").value = s.skipTypes;
$("enabled").onchange = () => chrome.storage.sync.set({ enabled: $("enabled").checked });
$("video").onchange = () => chrome.storage.sync.set({ videoButton: $("video").checked });
$("min").onchange = () => chrome.storage.sync.set({ minSizeMB: Math.max(0, Number($("min").value) || 0) });
$("skip").onchange = () => chrome.storage.sync.set({ skipTypes: $("skip").value });

// Site access (Firefox asks for it separately)
const all = { origins: ["<all_urls>"] };
if (chrome.permissions && !(await chrome.permissions.contains(all))) {
  $("access").hidden = false;
  $("grant").onclick = () => chrome.permissions.request(all).then((ok) => ($("access").hidden = ok));
}

// One link
$("send").onclick = async () => {
  const url = $("link").value.trim();
  if (!/^https?:\/\//i.test(url)) return;
  const r = await ask({ type: "send-link", url, referrer: tab && tab.url });
  $("link").value = r && r.ok ? "" : url;
};

function item(url, label, extra) {
  const li = document.createElement("li");
  const span = document.createElement("span");
  span.textContent = label;
  span.title = url;
  li.append(extra, span);
  return li;
}

// YouTube, Instagram, Facebook, X … : the app saves the page's video in the best quality
if (tab && isVideoSite(tab.url)) {
  $("siteBox").hidden = false;
  const go = (audioOnly, b) => async () => {
    const r = await ask({ type: "send-link", url: tab.url, referrer: tab.url, site: true, audioOnly });
    b.textContent = r && r.ok ? "Sent" : "Failed";
  };
  $("siteVideo").onclick = go(false, $("siteVideo"));
  $("siteAudio").onclick = go(true, $("siteAudio"));
}

// Media seen on this tab
if (tab) {
  const { media = [] } = (await ask({ type: "media-list", tabId: tab.id })) || {};
  $("noMedia").hidden = media.length > 0;
  for (const m of media) {
    const b = document.createElement("button");
    b.textContent = m.protected ? "Protected" : "Download";
    b.disabled = !!m.protected;
    b.title = m.protected ? "The site protects this video, so it can't be saved" : "Best quality";
    b.onclick = async () => {
      const msg = { type: "send-link", url: m.url, referrer: tab.url };
      if (m.kind === "stream") Object.assign(msg, { stream: true, fileName: m.name });
      const r = await ask(msg);
      b.textContent = r && r.ok ? "Sent" : "Failed";
    };
    const li = item(m.url, `${m.name} · ${qualityLabel(m)}`, document.createTextNode(""));
    li.append(b);
    $("media").append(li);
  }
}

// Links on this page
let found = [];
$("find").onclick = async () => {
  if (!tab) return;
  const { links = [] } = (await ask({ type: "page-links", tabId: tab.id })) || {};
  found = filterLinks(links, $("types").value);
  $("links").textContent = "";
  for (const url of found) {
    const box = document.createElement("input");
    box.type = "checkbox";
    box.checked = true;
    box.value = url;
    $("links").append(item(url, decodeURI(url.split("/").pop() || url), box));
  }
  $("links").hidden = $("linkActions").hidden = found.length === 0;
  if (!found.length) $("find").textContent = "None found";
};
const boxes = () => [...$("links").querySelectorAll("input")];
$("all").onclick = () => boxes().forEach((b) => (b.checked = true));
$("none").onclick = () => boxes().forEach((b) => (b.checked = false));
async function sendChecked(later) {
  const urls = boxes().filter((b) => b.checked).map((b) => b.value);
  if (!urls.length) return;
  const r = await ask({ type: "send-links", urls, later, referrer: tab && tab.url });
  $("download").textContent = r && r.ok ? `Sent ${r.sent}` : "Failed";
}
$("download").onclick = () => sendChecked(false);
$("later").onclick = () => sendChecked(true);

// App status
const r = await ask({ type: "ping" });
$("status").textContent = r && r.ok ? `Connected to the app (${r.version})` : "The app is not reachable. Open it once after installing.";
$("status").className = r && r.ok ? "ok" : "bad";
