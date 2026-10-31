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
