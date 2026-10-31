# Nexonic Download Manager

**A fast, free download manager for macOS and Windows, with a browser extension.** Files download over many connections at once,
pause and resume (even after a restart), and land sorted by type.

[![CI](https://github.com/Shahrukhalid/nexonic-download-manager/actions/workflows/ci.yml/badge.svg)](https://github.com/Shahrukhalid/nexonic-download-manager/actions/workflows/ci.yml)
![macOS 13+](https://img.shields.io/badge/macOS-13%2B-black)
![Windows 10+](https://img.shields.io/badge/Windows-10%2B-0078D6)
![Swift](https://img.shields.io/badge/Swift-5.9-orange)
[![MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

## Features

- **Multi-connection downloads:** up to 32 connections per file. When one part finishes, its connection takes over
  half of the largest part still downloading, so all connections stay busy until the end.
- **Pause and resume:** at any time, and after quitting the app or restarting the Mac. If the server changed the file,
  the download starts over instead of producing a broken file.
- **Reliable:** each part retries on its own (1, 2, 4 … up to 30 seconds, six times); the joined file is size-checked.
- **Queue and speed limit:** choose how many downloads run at once and cap the total speed.
- **Browser extension:** downloads you start in the browser open here, including ones that need you to be signed in.
  Right-click any link, video or audio and choose **Download with Nexonic**.
- **Organised:** All, In Progress, Unfinished, Finished and categories (Compressed, Documents, Music, Video, Programs,
  Images); optionally saved into category folders.
- **Mac-native:** SwiftUI, light and dark mode, notifications, a menu bar icon, a Dock badge with the active count,
  drag a link onto the window, paste several links at once.
- **Private:** no account, no tracking, nothing sent anywhere except the downloads you start.

## Install

1. Download the `.dmg` from [Releases](https://github.com/Shahrukhalid/nexonic-download-manager/releases) and drag the
   app to Applications.
2. The app is not notarized yet, so macOS asks before the first start:
   - **macOS 15:** open the app once, then go to **System Settings → Privacy & Security** and click **Open Anyway**.
   - **macOS 13 and 14:** right-click the app, choose **Open**, then **Open** again.

Requires macOS 13 Ventura or newer, on Apple silicon or Intel.

### Windows

Download `NexonicDownloadManager-Setup-<version>.exe` from Releases and run it (no administrator rights needed), or
use the portable zip. Windows may show "Windows protected your PC" because the installer is not signed yet: click
**More info → Run anyway**. Requires Windows 10 or 11 (64-bit). Closing the window keeps downloads running in the tray.

## Browser extension

Works in Chromium-based browsers (version 116 or newer).

1. Open the app once (this connects it to the installed browsers).
2. Mac: **Settings → Browser → Save Extension Folder…** (or the **Browser Extension** folder on the `.dmg`).
   Windows: the **Extension** folder inside the app's install folder.
3. In the browser, open the extensions page, switch on **Developer mode**, click **Load unpacked** and select that folder.

The popup shows whether the app is reachable, lets you switch the hand-off on or off, set the smallest file size to
take over, and list file types that should stay in the browser. If the app can't be reached, the browser finishes the
download itself.

The extension asks for: downloads (to take them over), native messaging (to talk to the app), context menus, storage
(its settings), cookies and all sites (so downloads that need a sign-in keep working). Cookies are only sent to the
site the download comes from.

## Build from source

```bash
git clone https://github.com/Shahrukhalid/nexonic-download-manager.git && cd nexonic-download-manager
swift test
UNIVERSAL=0 scripts/build-app.sh      # build/Nexonic Download Manager.app and a .dmg
```

Universal builds (Apple silicon and Intel) need Xcode; set `SIGN_IDENTITY` to sign with a Developer ID certificate.

Windows (needs the .NET 8 SDK; the installer needs Inno Setup 6): `pwsh windows/build.ps1`.

## How it works

- `Sources/NexonicCore`: the download engine (probe, range planning, parallel parts, splitting, retries, resume,
  speed limit, file names and categories) and the shared pieces of the browser link.
- `Sources/NexonicDownloadManager`: the SwiftUI app.
- `Sources/NexonicHost`: a small helper inside the app that the browser talks to; it only accepts http and https
  links and hands them to the app.
- `windows/`: the Windows app (C#, WPF): the same engine in `Core`, the window in `App`, the helper in `Host`,
  tests and the installer script (`windows/build.ps1`).
- `extension/`: the browser extension (shared by both).

Data lives in `~/Library/Application Support/Nexonic Download Manager/`.

## Roadmap

Scheduler, picking media files from a page, checksum check, proxy and per-site limits, an extension store listing and
a notarized build. Suggestions and pull requests are welcome.

## License

[MIT](LICENSE)
