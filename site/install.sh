#!/bin/bash
# Installs or updates Nexonic Download Manager on a Mac:
#   curl -fsSL https://shahrukhalid.github.io/nexonic-download-manager/install.sh | bash
# Downloads the latest release from GitHub, copies the app into /Applications and opens it.
set -euo pipefail

APP_NAME="Nexonic Download Manager"
DMG_URL="https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest/download/NexonicDownloadManager-Mac.dmg"
TERMS_URL="https://github.com/Shahrukhalid/nexonic-download-manager/blob/main/TERMS.md"
DEST="/Applications"

if [ "$(uname -s)" != "Darwin" ]; then
  echo "This installer is for macOS. Windows: download the Setup file from"
  echo "https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest"
  exit 1
fi

echo "Installing $APP_NAME (free and open source, MIT license)."
echo "By installing you agree to the Terms of Use: $TERMS_URL"

work="$(mktemp -d)"
mnt="$work/mnt"
cleanup() {
  hdiutil detach "$mnt" -quiet >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

echo "Downloading the latest version…"
curl -fL --progress-bar -o "$work/app.dmg" "$DMG_URL"

mkdir -p "$mnt"
# The disk image shows the Terms of Use; agreeing to them was confirmed above. (Fed from `yes` without a pipe:
# with pipefail, `yes` ending on SIGPIPE would otherwise stop the script here.)
if ! hdiutil attach "$work/app.dmg" -nobrowse -noautoopen -quiet -mountpoint "$mnt" < <(yes) >/dev/null 2>&1; then
  echo "Could not open the downloaded disk image. Please try again." >&2
  exit 1
fi

if [ ! -d "$mnt/$APP_NAME.app" ]; then
  echo "The download did not contain the app. Please try again later." >&2
  exit 1
fi

if pgrep -xq "NexonicDownloadManager"; then
  echo "Closing the running copy…"
  osascript -e "tell application \"$APP_NAME\" to quit" >/dev/null 2>&1 || true
  sleep 2
fi

target="$DEST/$APP_NAME.app"
if [ ! -w "$DEST" ]; then
  echo "Administrator password needed to copy the app into $DEST."
  sudo rm -rf "$target"
  sudo ditto "$mnt/$APP_NAME.app" "$target"
  sudo xattr -dr com.apple.quarantine "$target" 2>/dev/null || true
else
  rm -rf "$target"
  ditto "$mnt/$APP_NAME.app" "$target"
  xattr -dr com.apple.quarantine "$target" 2>/dev/null || true
fi

version="$(defaults read "$target/Contents/Info" CFBundleShortVersionString 2>/dev/null || echo "")"
echo "Installed $APP_NAME ${version} in $DEST."
echo "Browser extensions: open the app, then Settings > Browsers & Command Line."
open "$target"
