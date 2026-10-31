import { DEFAULTS, HOST, addMedia, cookieHeader, header, isMedia, nameFromUrl, shouldTakeOver } from "./helpers.js";

async function settings() {
  return { ...DEFAULTS, ...(await chrome.storage.sync.get(Object.keys(DEFAULTS))) };
}

async function send(url, extra = {}) {
  let cookies = "";
  try {
    cookies = cookieHeader(await chrome.cookies.getAll({ url }));
  } catch (_) {
    /* no cookie access for this address */
  }
  const message = { type: "download", url, cookies, userAgent: navigator.userAgent, ...extra };
  try {
    const reply = await chrome.runtime.sendNativeMessage(HOST, message);
    const ok = !!(reply && reply.ok);
    await chrome.action.setBadgeText({ text: ok ? "" : "!" });
    return ok;
  } catch (_) {
    await chrome.action.setBadgeText({ text: "!" });
    return false;
  }
}

// ---- Browser downloads go to the app ----

chrome.downloads.onCreated.addListener(async (item) => {
  if (!shouldTakeOver(item, await settings())) return;
  try {
    await chrome.downloads.pause(item.id);
  } catch (_) {
    return; // already finished or not pausable
  }
  const url = item.finalUrl || item.url;
  const name = item.filename ? item.filename.split(/[\\/]/).pop() : undefined;
  const ok = await send(url, { referrer: item.referrer || "", fileName: name });
  if (ok) {
    await chrome.downloads.cancel(item.id).catch(() => {});
    await chrome.downloads.erase({ id: item.id }).catch(() => {});
  } else {
    await chrome.downloads.resume(item.id).catch(() => {}); // the app is not reachable: let the browser finish
  }
});

// ---- Right-click menu ----

chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({ id: "nexonic-download", title: "Download with Nexonic", contexts: ["link", "video", "audio"] });
  chrome.contextMenus.create({ id: "nexonic-all", title: "Download all links with Nexonic", contexts: ["page", "selection"] });
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  const referrer = info.pageUrl || (tab && tab.url) || "";
  if (info.menuItemId === "nexonic-all" && tab) {
    const links = await pageLinks(tab.id, !!info.selectionText);
    for (const url of links) await send(url, { referrer });
    return;
  }
  const url = info.linkUrl || info.srcUrl;
  if (url && /^https?:\/\//i.test(url)) send(url, { referrer });
});

/** Links on a page (only inside the selection when there is one). */
async function pageLinks(tabId, selectionOnly) {
  const [result] = await chrome.scripting.executeScript({
    target: { tabId },
    args: [selectionOnly],
    func: (onlySelected) => {
      const anchors = [...document.querySelectorAll("a[href]")];
      const sel = window.getSelection();
      const keep = onlySelected && sel && sel.rangeCount ? anchors.filter((a) => sel.containsNode(a, true)) : anchors;
      return keep.map((a) => a.href);
    },
  });
  return (result && result.result) || [];
}

// ---- Audio and video seen on each tab ----

async function mediaOf(tabId) {
  const key = `media-${tabId}`;
  return (await chrome.storage.session.get(key))[key] || [];
}

async function setMedia(tabId, list) {
  await chrome.storage.session.set({ [`media-${tabId}`]: list });
}

chrome.webRequest.onResponseStarted.addListener(
  async (d) => {
    if (d.tabId < 0 || d.statusCode >= 400) return;
    const type = header(d.responseHeaders, "content-type");
    const range = header(d.responseHeaders, "content-range");
    const total = range.includes("/") ? Number(range.split("/").pop()) : Number(header(d.responseHeaders, "content-length") || -1);
    if (!isMedia(d.url, type, Number.isFinite(total) ? total : -1)) return;
    const item = { url: d.url, name: nameFromUrl(d.url), size: total > 0 ? total : -1, type: type.split(";")[0] };
    await setMedia(d.tabId, addMedia(await mediaOf(d.tabId), item));
  },
  { urls: ["<all_urls>"], types: ["media", "xmlhttprequest", "other", "object"] },
  ["responseHeaders"],
);

chrome.tabs.onUpdated.addListener((tabId, change) => {
  if (change.status === "loading" && change.url) setMedia(tabId, []);
});
chrome.tabs.onRemoved.addListener((tabId) => chrome.storage.session.remove(`media-${tabId}`));

// ---- Messages from the popup and the page button ----

chrome.runtime.onMessage.addListener((msg, sender, respond) => {
  const tabId = msg.tabId ?? (sender.tab && sender.tab.id);
  const referrer = msg.referrer || (sender.tab && sender.tab.url) || "";
  const run = async () => {
    switch (msg && msg.type) {
      case "send-link":
        return { ok: await send(msg.url, { referrer, later: !!msg.later }) };
      case "send-links": {
        let sent = 0;
        for (const url of msg.urls || []) if (await send(url, { referrer, later: !!msg.later })) sent++;
        return { ok: sent > 0, sent };
      }
      case "media-list":
        return { media: await mediaOf(tabId) };
      case "page-links":
        return { links: await pageLinks(tabId, false) };
      case "video-button": {
        // The page's own address when it is a plain file, otherwise what the tab has loaded.
        if (/^https?:\/\//i.test(msg.src || "")) return { media: [{ url: msg.src, name: nameFromUrl(msg.src), size: -1 }] };
        return { media: await mediaOf(tabId) };
      }
      case "ping": {
        const r = await chrome.runtime.sendNativeMessage(HOST, { type: "ping" }).catch(() => null);
        return { ok: !!(r && r.ok), version: r && r.version };
      }
      default:
        return { ok: false };
    }
  };
  run().then(respond, () => respond({ ok: false }));
  return true;
});
