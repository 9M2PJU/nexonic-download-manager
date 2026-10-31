import { DEFAULTS, HOST, cookieHeader, shouldTakeOver } from "./helpers.js";

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

chrome.runtime.onInstalled.addListener(() => {
  chrome.contextMenus.create({
    id: "nexonic-download",
    title: "Download with Nexonic",
    contexts: ["link", "video", "audio"],
  });
});

chrome.contextMenus.onClicked.addListener((info, tab) => {
  const url = info.linkUrl || info.srcUrl;
  if (url && /^https?:\/\//i.test(url)) send(url, { referrer: info.pageUrl || (tab && tab.url) || "" });
});

chrome.runtime.onMessage.addListener((msg, _sender, respond) => {
  if (msg && msg.type === "send-link") {
    send(msg.url).then((ok) => respond({ ok }));
    return true;
  }
  if (msg && msg.type === "ping") {
    chrome.runtime
      .sendNativeMessage(HOST, { type: "ping" })
      .then((r) => respond({ ok: !!(r && r.ok), version: r && r.version }))
      .catch(() => respond({ ok: false }));
    return true;
  }
  return false;
});
