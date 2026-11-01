<div align="center">

# Nexonic Download Manager

**Fast, free download manager for Mac and Windows, with browser extensions for Chrome, Edge, Brave and Firefox.**

A free, open-source alternative to Internet Download Manager (IDM), for Mac as well as Windows.

Splits every download into up to 32 connections · pauses and resumes, even after a restart · queues and a scheduler ·
catches downloads and videos from your browser

[![CI](https://github.com/Shahrukhalid/nexonic-download-manager/actions/workflows/ci.yml/badge.svg)](https://github.com/Shahrukhalid/nexonic-download-manager/actions/workflows/ci.yml)
[![Latest release](https://img.shields.io/github/v/release/Shahrukhalid/nexonic-download-manager?label=download)](https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest)
![macOS 13+](https://img.shields.io/badge/macOS-13%2B-black)
![Windows 10+](https://img.shields.io/badge/Windows-10%2B-0078D6)
[![MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

</div>

### Video and audio

Point at a video or audio player on a web page and click **Download this video**, or open the extension popup to see
every video and audio file the page has loaded. This works on sites that play ordinary video files (news sites, course
platforms, blogs, file hosts, your own media). Large video and social platforms usually stream video in many small
encrypted or split pieces, which the app can't save as one file. Only download what you have the right to save.

<sub>Nexonic Download Manager is an independent project and is not affiliated with Internet Download Manager or its
makers.</sub>

## Download

| Your computer | Download | Size |
|---|---|---|
| **Mac** (Apple silicon and Intel, macOS 13 or newer) | [**NexonicDownloadManager-Mac.dmg**](https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest/download/NexonicDownloadManager-Mac.dmg) | about 1 MB |
| **Windows** 10 or 11, installer (recommended) | [**NexonicDownloadManager-Windows-Setup.exe**](https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest/download/NexonicDownloadManager-Windows-Setup.exe) | about 70 MB |
| **Windows**, portable (no install, runs from a folder) | [NexonicDownloadManager-Windows-Portable.zip](https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest/download/NexonicDownloadManager-Windows-Portable.zip) | about 90 MB |
| **Chrome, Edge, Brave, Vivaldi, Opera** extension | [NexonicExtension-Chrome.zip](https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest/download/NexonicExtension-Chrome.zip) (also inside the apps) | small |
| **Firefox** add-on | [NexonicAddon-Firefox.xpi](https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest/download/NexonicAddon-Firefox.xpi) (also inside the apps) | small |

The links always point to the newest version. Older versions and release notes:
[all releases](https://github.com/Shahrukhalid/nexonic-download-manager/releases).

> **Why is the Mac file so much smaller?** macOS already includes everything a Mac app needs to run, so the `.dmg`
> only holds the app itself. The Windows files carry their own copy of the .NET runtime, so they work on any Windows
> 10 or 11 PC without installing anything else. Both have exactly the same features.

## Install in 2 minutes

### Mac

1. Download **NexonicDownloadManager-Mac.dmg** and open it.
2. Drag **Nexonic Download Manager** onto **Applications**.
3. Open it from Applications. The first time, macOS asks because the app is not notarized yet:
   - **macOS 15 or newer:** click **Done**, open **System Settings → Privacy & Security**, scroll down and click
     **Open Anyway**, then **Open**.
   - **macOS 13 or 14:** right-click the app, choose **Open**, then **Open** again.
4. You'll see the app window and a download icon in the menu bar. Closing the window keeps downloads running.

### Windows

1. Download **NexonicDownloadManager-Windows-Setup.exe** and run it. No administrator rights are needed.
2. If Windows shows "Windows protected your PC" (the installer is not signed yet), click **More info → Run anyway**.
3. Keep **Add the ndm command** ticked if you want the command line, then **Install**.
4. The app opens; closing the window keeps downloads running in the tray (bottom-right, next to the clock).

Portable instead: unzip **NexonicDownloadManager-Windows-Portable.zip** anywhere and run `NexonicDownloadManager.exe`.

### Browser (so downloads from websites go to the app)

Open the app once first, so the browser can find it.

**Chrome, Edge, Brave, Vivaldi, Opera**

1. Get the extension folder: Mac **Options → Browsers & Command Line → Save Extension Folder…**; Windows: the
   **Extension** folder in the install folder (**Options → Browsers & Command Line → Open the App Folder**). You can
   also unzip **NexonicExtension-Chrome.zip**.
2. In the browser go to the extensions page (`chrome://extensions`, `edge://extensions`, `brave://extensions`).
3. Switch on **Developer mode** (top right), click **Load unpacked** and pick the folder.
4. Pin the Nexonic icon. Its popup says **Connected to the app** when everything works.

**Firefox**

Firefox only installs signed add-ons. To sign your copy once: upload **NexonicAddon-Firefox.xpi** to Mozilla's
add-on developer hub as a self-distributed ("On your own") add-on, then drag the signed `.xpi` it gives back onto a
Firefox window. For a quick test without signing: `about:debugging` → **This Firefox** → **Load Temporary Add-on…**
(it stays until Firefox restarts).

## Using it

| To | Do this |
|---|---|
| Download a link | **Add URL** (or Ctrl/⌘+N), paste one or more links, **Start Download**. |
| Download from a website | Just click the download link in the browser; the app takes it over. |
| Save a video or song from a page | Point at the player and click **Download this video**, or open the extension popup → **Media on this page**. |
| Get many files from one page | Extension popup → **Links on this page** → type the file types → tick → **Download**. |
| Download a numbered series | **Batch** → `https://example.com/img[001-100].jpg`. |
| Download a whole folder of a site | **Grabber** → start page, depth, file types → **Explore** → **Download Checked**. |
| Download at night | **Scheduler** → **New Queue** → set **Start at**, days and **When the queue is done**; add downloads with **Download Later** into that queue. |
| Continue an expired link | Right-click → **Refresh Link…** → paste the new link, or **Wait for Browser** and start the download again in the browser. |
| Check a file | Add the expected checksum in **Add URL** or **Properties**; it is checked when the download finishes. |
| Limit speed | **Options → Connection** for everything; **Properties** for one download. |

The toolbar is the same on Mac and Windows: **Add URL · Resume · Stop · Stop All · Delete · Delete Completed ·
Scheduler · Start Queue · Stop Queue · Grabber · Batch · Options**, plus a search box. The sidebar groups downloads by
state, file type and queue, and the status bar shows how many are running and the total speed.

## Features

- **Fast:** up to 32 connections per file. When a part finishes, its connection takes over half of the largest part
  still downloading, so every connection stays busy until the end. Failed parts retry on their own.
- **Pause and resume** at any time, also after quitting or restarting. If the server changed the file, the download
  starts over instead of producing a broken file.
- **Queues and scheduler:** start and stop times, weekdays, files at the same time, and quit, sleep or shut down when
  done.
- **Clipboard:** copy a link to a file (zip, mp4, pdf … the list is yours) and the Add window opens with it.
- **Proxy and passwords:** system, HTTP or SOCKS5 proxy (with optional login); per-download user name and password.
- **After a download:** Download Complete window (Open, Open Folder), sound, notification, checksum (MD5, SHA-1,
  SHA-256, SHA-512), optional virus scan, and the file is marked as downloaded so the system checks it when opened.
- **Categories:** Compressed, Documents, Music, Video, Programs, Images; optionally saved into category folders.
- **Private:** no account, no tracking, nothing sent anywhere except the downloads you start.

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

## Troubleshooting

- **The extension popup says the app is not reachable:** open the app once, then restart the browser.
- **A download fails with "link has expired":** use **Refresh Link…** (right-click the download).
- **A site only allows one connection:** the details panel shows "Resume: no"; the download still works, just
  without splitting.
- **Firefox says the add-on is corrupt or unverified:** it needs signing first (see above).

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
