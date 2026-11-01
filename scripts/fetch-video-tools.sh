#!/usr/bin/env bash
# Downloads the video engine shipped with the app: the official yt-dlp release and QuickJS (a small JavaScript
# runtime yt-dlp needs for some sites).
#   scripts/fetch-video-tools.sh mac     OUT_DIR   # yt-dlp (universal) + qjs (universal, needs lipo)
#   scripts/fetch-video-tools.sh windows OUT_DIR   # yt-dlp.exe + qjs.exe
set -euo pipefail
TARGET="$1"
OUT="$(mkdir -p "$2" && cd "$2" && pwd)"
QJS=v0.16.1
YT=https://github.com/yt-dlp/yt-dlp/releases/latest/download
QJ="https://github.com/quickjs-ng/quickjs/releases/download/$QJS"
get() { curl -fsSL --retry 4 -o "$2" "$1"; }

case "$TARGET" in
  mac)
    get "$YT/yt-dlp_macos" "$OUT/yt-dlp"
    get "$QJ/qjs-darwin-arm64" "$OUT/qjs-arm64"
    get "$QJ/qjs-darwin-x86_64" "$OUT/qjs-x86_64"
    lipo -create "$OUT/qjs-arm64" "$OUT/qjs-x86_64" -output "$OUT/qjs"
    rm "$OUT/qjs-arm64" "$OUT/qjs-x86_64"
    chmod +x "$OUT/yt-dlp" "$OUT/qjs"
    ;;
  windows)
    get "$YT/yt-dlp.exe" "$OUT/yt-dlp.exe"
    get "$QJ/qjs-windows-x86_64.exe" "$OUT/qjs.exe"
    ;;
  *) echo "usage: $0 mac|windows OUT_DIR" >&2; exit 2 ;;
esac

cat > "$OUT/VIDEO-ENGINE-LICENSES.txt" <<TXT
This app includes:
- yt-dlp (public domain, The Unlicense): https://github.com/yt-dlp/yt-dlp
  The app keeps its own copy up to date once a day, because video sites change often.
- QuickJS-ng $QJS (MIT License): https://github.com/quickjs-ng/quickjs
They are used unmodified.
TXT
echo "Fetched the video engine for $TARGET into $OUT"
