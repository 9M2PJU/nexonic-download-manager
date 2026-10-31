import assert from "node:assert/strict";
import test from "node:test";
import { cookieHeader, extensionOf, shouldTakeOver } from "./helpers.js";

test("takes over normal web downloads only", () => {
  const big = { url: "https://example.com/a.zip", filename: "/Users/x/Downloads/a.zip", totalBytes: 50e6 };
  assert.equal(shouldTakeOver(big, {}), true);
  assert.equal(shouldTakeOver({ ...big, url: "blob:https://example.com/1" }, {}), false);
  assert.equal(shouldTakeOver({ ...big, totalBytes: 1000 }, {}), false);
  assert.equal(shouldTakeOver({ ...big, totalBytes: -1 }, {}), true);
  assert.equal(shouldTakeOver(big, { enabled: false }), false);
  assert.equal(shouldTakeOver(big, { skipTypes: ".ZIP, pdf" }), false);
});

test("file extensions", () => {
  assert.equal(extensionOf("https://e.com/x/Movie.MP4?token=1"), "mp4");
  assert.equal(extensionOf("/Users/x/Downloads/archive.tar.gz"), "gz");
  assert.equal(extensionOf("README"), "");
});

test("cookie header", () => {
  assert.equal(cookieHeader([{ name: "a", value: "1" }, { name: "b", value: "2" }]), "a=1; b=2");
  assert.equal(cookieHeader([]), "");
});

import { addMedia, filterLinks, formatSize, isMedia, nameFromUrl } from "./helpers.js";

test("media detection", () => {
  assert.equal(isMedia("https://e.com/v/clip.mp4", "", -1), true);
  assert.equal(isMedia("https://e.com/stream", "video/webm", 5e6), true);
  assert.equal(isMedia("https://e.com/live/index.m3u8", "application/vnd.apple.mpegurl", -1), false);
  assert.equal(isMedia("https://e.com/seg/001.ts", "video/mp2t", 1e6), false);
  assert.equal(isMedia("https://e.com/tiny.mp3", "audio/mpeg", 1000), false);
  assert.equal(isMedia("blob:https://e.com/1", "video/mp4", -1), false);
});

test("link filtering", () => {
  const links = ["https://e.com/a.zip", "https://e.com/a.zip#x", "https://e.com/page.html", "https://e.com/b.PDF", "mailto:x@e.com", "https://e.com/dir/"];
  assert.deepEqual(filterLinks(links, ""), ["https://e.com/a.zip", "https://e.com/b.PDF"]);
  assert.deepEqual(filterLinks(links, "pdf"), ["https://e.com/b.PDF"]);
});

test("small helpers", () => {
  assert.equal(nameFromUrl("https://e.com/x/My%20Song.mp3?a=1"), "My Song.mp3");
  assert.equal(formatSize(1536), "1.5 KB");
  assert.equal(formatSize(-1), "");
  const list = addMedia(addMedia([], { url: "a" }), { url: "b" });
  assert.deepEqual(addMedia(list, { url: "a" }).map((m) => m.url), ["a", "b"]);
});
