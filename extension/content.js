// "Download this video" button over audio and video players on web pages.
(() => {
  if (window.__nexonicVideoButton) return;
  window.__nexonicVideoButton = true;

  let enabled = true;
  chrome.storage.sync.get({ videoButton: true }, (s) => (enabled = s.videoButton));
  chrome.storage.onChanged.addListener((c) => {
    if (c.videoButton) enabled = c.videoButton.newValue;
  });

  const host = document.createElement("div");
  host.style.cssText = "position:fixed;z-index:2147483647;top:0;left:0;display:none";
  const root = host.attachShadow({ mode: "closed" });
  root.innerHTML = `
    <style>
      button{font:600 12px system-ui,sans-serif;color:#fff;background:#008f9e;border:0;border-radius:6px;padding:6px 10px;
        cursor:pointer;box-shadow:0 2px 8px rgba(0,0,0,.35)}
      button:hover{background:#00727e}
      ul{list-style:none;margin:4px 0 0;padding:4px;background:#fff;color:#111;border-radius:6px;max-width:360px;
        box-shadow:0 2px 10px rgba(0,0,0,.35);font:12px system-ui,sans-serif}
      li{padding:5px 8px;border-radius:4px;cursor:pointer;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
      li:hover{background:#e3f4f6}
      .note{color:#666;cursor:default}
    </style>
    <button type="button">Download this video</button>
    <ul hidden></ul>`;
  const button = root.querySelector("button");
  const list = root.querySelector("ul");
  let target = null;
  let hideTimer = 0;

  const size = (n) => (n > 0 ? ` (${(n / 1048576).toFixed(1)} MB)` : "");

  function place() {
    if (!target || !target.isConnected) return hide();
    const r = target.getBoundingClientRect();
    if (r.width < 160 || r.height < 40 || r.bottom < 0 || r.top > innerHeight) return hide();
    host.style.top = `${Math.max(4, r.top + 8)}px`;
    host.style.left = `${Math.min(innerWidth - 190, Math.max(4, r.right - 180))}px`;
    host.style.display = "block";
  }

  function hide() {
    host.style.display = "none";
    list.hidden = true;
    target = null;
  }

  function show(el) {
    if (!enabled) return;
    clearTimeout(hideTimer);
    target = el;
    button.textContent = el.tagName === "AUDIO" ? "Download this audio" : "Download this video";
    if (!host.isConnected) document.documentElement.appendChild(host);
    place();
  }

  function send(url) {
    chrome.runtime.sendMessage({ type: "send-link", url, referrer: location.href }, (r) => {
      button.textContent = r && r.ok ? "Sent to Nexonic" : "Open Nexonic first";
      setTimeout(hide, 1500);
    });
  }

  button.addEventListener("click", (e) => {
    e.preventDefault();
    e.stopPropagation();
    const src = target && (target.currentSrc || target.src);
    chrome.runtime.sendMessage({ type: "video-button", src }, (r) => {
      const media = (r && r.media) || [];
      list.textContent = "";
      if (media.length === 1) return send(media[0].url);
      if (!media.length) {
        const li = document.createElement("li");
        li.className = "note";
        li.textContent = "This player streams in small pieces, which can't be saved yet. Play the video for a moment and try again.";
        li.style.whiteSpace = "normal";
        list.appendChild(li);
      }
      for (const m of media) {
        const li = document.createElement("li");
        li.textContent = `${m.name}${size(m.size)}`;
        li.title = m.url;
        li.onclick = () => send(m.url);
        list.appendChild(li);
      }
      list.hidden = false;
    });
  });

  host.addEventListener("mouseenter", () => clearTimeout(hideTimer));
  host.addEventListener("mouseleave", () => (hideTimer = setTimeout(hide, 800)));
  document.addEventListener(
    "mouseover",
    (e) => {
      const el = e.target && e.target.closest ? e.target.closest("video, audio") : null;
      if (el) show(el);
      else if (target && !host.contains(e.target)) {
        const r = target.getBoundingClientRect();
        const inside = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
        if (!inside) {
          clearTimeout(hideTimer);
          hideTimer = setTimeout(hide, 800);
        }
      }
    },
    true,
  );
  addEventListener("scroll", place, true);
  addEventListener("resize", place);
})();
