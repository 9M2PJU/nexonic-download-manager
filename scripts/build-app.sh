#!/usr/bin/env bash
# Builds build/Nexonic Download Manager.app, a .dmg and the extension zip.
#   UNIVERSAL=0 scripts/build-app.sh      # this Mac's architecture only (faster)
#   SIGN_IDENTITY="Developer ID Application: …" scripts/build-app.sh   # signed with hardened runtime
set -euo pipefail
cd "$(dirname "$0")/.."
VERSION="$(cat VERSION)"
APP="build/Nexonic Download Manager.app"
ARCHS=()
if [ "${UNIVERSAL:-1}" != "0" ] && xcodebuild -version >/dev/null 2>&1; then ARCHS=(--arch arm64 --arch x86_64); fi

swift build -c release "${ARCHS[@]}"
BIN="$(swift build -c release "${ARCHS[@]}" --show-bin-path)"

rm -rf build && mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN/NexonicDownloadManager" "$APP/Contents/MacOS/"
cp "$BIN/nexonic-host" "$APP/Contents/MacOS/"
sed "s/__VERSION__/$VERSION/g" packaging/Info.plist > "$APP/Contents/Info.plist"
cp -R extension "$APP/Contents/Resources/Extension"
rm -f "$APP/Contents/Resources/Extension/"{helpers.test.js,package.json}

ICONSET="build/AppIcon.iconset"
mkdir -p "$ICONSET"
for s in 16 32 128 256 512; do
  sips -z $s $s packaging/icon-1024.png --out "$ICONSET/icon_${s}x${s}.png" >/dev/null
  sips -z $((s * 2)) $((s * 2)) packaging/icon-1024.png --out "$ICONSET/icon_${s}x${s}@2x.png" >/dev/null
done
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/AppIcon.icns"

if [ -n "${SIGN_IDENTITY:-}" ]; then
  codesign --force --options runtime --timestamp -s "$SIGN_IDENTITY" "$APP/Contents/MacOS/nexonic-host"
  codesign --force --options runtime --timestamp -s "$SIGN_IDENTITY" "$APP"
else
  codesign --force --deep -s - "$APP"
fi

STAGE="build/dmg"
mkdir -p "$STAGE"
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"
cp -R "$APP/Contents/Resources/Extension" "$STAGE/Browser Extension"
hdiutil create -volname "Nexonic Download Manager" -srcfolder "$STAGE" -ov -format UDZO \
  "build/NexonicDownloadManager-$VERSION.dmg" >/dev/null
(cd "$APP/Contents/Resources/Extension" && zip -qr "../../../../NexonicExtension-$VERSION.zip" .)
echo "Built $APP, build/NexonicDownloadManager-$VERSION.dmg and build/NexonicExtension-$VERSION.zip"
