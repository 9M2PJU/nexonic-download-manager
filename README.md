# Nexonic Download Manager

**A fast, free download manager for macOS and Windows, with a browser extension.** Files download over many connections at once,
pause and resume (even after a restart), and land sorted by type.

[![CI](https://github.com/Shahrukhalid/nexonic-download-manager/actions/workflows/ci.yml/badge.svg)](https://github.com/Shahrukhalid/nexonic-download-manager/actions/workflows/ci.yml)
![macOS 13+](https://img.shields.io/badge/macOS-13%2B-black)
![Windows 10+](https://img.shields.io/badge/Windows-10%2B-0078D6)
![Swift](https://img.shields.io/badge/Swift-5.9-orange)
[![MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

## Features

The Mac and Windows apps have the same features, windows and toolbar, so the steps below work on both.

| Toolbar | What it does |
|---|---|
| **Add URL** | Paste one or more links. Choose the file name, folder and queue, add a user name and password, an expected checksum or a speed limit, then **Start Download** or **Download Later**. |
| **Resume / Stop / Stop All** | Pause and continue at any time, also after quitting the app or restarting the computer. |
| **Delete / Delete Completed** | Clean up the list (right-click also offers moving the file to the Trash or Recycle Bin). |
| **Scheduler** | Queues with start and stop times, weekdays, how many files run at once, and what to do when done: quit, sleep or shut down. |
| **Start Queue / Stop Queue** | Run or stop the queue selected in the sidebar. |
| **Grabber** | Explores a website section (up to five links deep) and lists its files by type; tick the ones you want. |
| **Batch** | Numbered series: `https://example.com/img[001-100].jpg`, `[a-z]`, `[0-100:5]`; several ranges multiply. |
| **Options** | Connections, folders, speed limit, proxy, clipboard, after-download actions, browsers and the command line. |

- **Fast:** up to 32 connections per file. When a part finishes, its connection takes over half of the largest part
  still downloading, so every connection stays busy until the end. Failed parts retry on their own.
- **Queues:** the main queue starts downloads straight away; add more queues (for example "Night") with their own
  timetable and limit. Move downloads between queues from the right-click menu.
- **Clipboard:** copy a link to a file (zip, mp4, pdf … the list is yours) and the Add window opens with it.
- **Refresh Link:** when a link expires, paste a fresh one, or choose **Wait for Browser** and start the download again
  in the browser. It continues from where it stopped.
- **Proxy and passwords:** system, HTTP or SOCKS5 proxy (with optional login); per-download user name and password.
- **Speed limits:** for everything, and for each download (Properties).
- **After a download:** Download Complete window (Open, Open Folder), a sound, a notification, a checksum check
  (MD5, SHA-1, SHA-256, SHA-512), an optional virus scan, and the file is marked as downloaded so the system checks it
  when first opened.
- **Categories:** Compressed, Documents, Music, Video, Programs, Images; optionally saved into category folders.
- **Command line:** `ndm` adds downloads from scripts (see below).
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

## Browser extension and Firefox add-on

**Chrome, Edge, Brave, Vivaldi, Opera** (version 116 or newer):

1. Open the app once (this connects it to the installed browsers).
2. Mac: **Options → Browsers & Command Line → Save Extension Folder…** (or the **Browser Extension** folder on the
   `.dmg`). Windows: the **Extension** folder inside the app's install folder.
3. In the browser, open the extensions page, switch on **Developer mode**, click **Load unpacked** and select that folder.

**Firefox** (version 121 or newer): use `NexonicFirefoxAddon-<version>.xpi` from Releases (also inside the app: Mac
**Options → Browsers & Command Line → Save Firefox Add-on…**, Windows `Firefox Add-on.xpi` in the install folder).
Firefox only installs signed add-ons: upload the `.xpi` once to Mozilla's add-on developer hub as a self-distributed
("On your own") add-on and install the signed file it returns. Firefox Developer Edition and Nightly can also load it
unsigned for testing (about:debugging → Load Temporary Add-on).

What the extension does:

- Downloads you start in the browser open in the app, including ones that need you to be signed in.
- **Download this video:** a button appears over audio and video players. If the player streams in many small pieces
  (common on large video sites), the button lists the whole files the page has loaded, when there are any.
- The popup lists **media on this page** and **links on this page** (filter by type, tick, Download or Download later).
- Right-click: **Download with Nexonic** on a link, video or audio, and **Download all links with Nexonic** on a page
  or selection.
- If the app can't be reached, the browser finishes the download itself.

It asks for: downloads, native messaging (to talk to the app), context menus, storage (its settings), cookies, tabs,
scripting and web requests (to find links and media on the page) and all sites. Cookies are only sent to the site the
download comes from.

## Command line

Mac: **Options → Browsers & Command Line → Install ndm Command…** (asks for your password once). Windows: the installer
adds `ndm` (open a new terminal afterwards).

```text
ndm https://example.com/file.zip
ndm https://example.com/a.iso https://example.com/b.iso --queue Night --later
ndm batch "https://example.com/img[001-100].jpg" --folder ~/Pictures
ndm https://example.com/private.zip --user me --password secret --checksum <sha256> --limit 500
ndm help
```

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
  speed limits, proxy and login, checksums), queues and timetables, batch patterns, the site grabber, the `ndm`
  command and the shared pieces of the browser link.
- `Sources/NexonicDownloadManager`: the SwiftUI app.
- `Sources/NexonicHost`: a small helper inside the app that the browser talks to, and the `ndm` command; it only
  accepts http and https links and hands them to the app.
- `windows/`: the Windows app (C#, WPF): the same engine in `Core`, the window in `App`, the helper in `Host`,
  tests and the installer script (`windows/build.ps1`).
- `extension/`: the browser extension; `scripts/build-extensions.py` builds the Chrome version and the Firefox add-on.

Data lives in `~/Library/Application Support/Nexonic Download Manager/` (Mac) and
`%LOCALAPPDATA%\Nexonic Download Manager\` (Windows), readable only by your user account.

## Roadmap

Saving streamed video (playlists of small pieces), periodic folder synchronisation, extension store listings and a
notarized, signed build. Suggestions and pull requests are welcome.

## License

[MIT](LICENSE)
