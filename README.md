<div align="center">

# Nexonic Download Manager

**Free, open-source download manager for Mac and Windows: an alternative to Internet Download Manager (IDM).**

Download faster, pause and resume anytime, and save videos from YouTube, Instagram, Facebook, X (Twitter), TikTok and
1,000+ other sites in the best quality.

[![Latest release](https://img.shields.io/github/v/release/Shahrukhalid/nexonic-download-manager?label=download)](https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest)
![macOS 13+](https://img.shields.io/badge/macOS-13%2B-black)
![Windows 10+](https://img.shields.io/badge/Windows-10%2B-0078D6)
[![MIT](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

<img src="https://raw.githubusercontent.com/Shahrukhalid/nexonic-download-manager/screenshots/mac-main.png" alt="Nexonic Download Manager" width="880">

</div>

## Download

| System | File |
|---|---|
| **Mac** (macOS 13+, Apple silicon and Intel) | [NexonicDownloadManager-Mac.dmg](https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest/download/NexonicDownloadManager-Mac.dmg) |
| **Windows** 10 / 11 | [NexonicDownloadManager-Windows-Setup.exe](https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest/download/NexonicDownloadManager-Windows-Setup.exe) |
| Windows, no install | [NexonicDownloadManager-Windows-Portable.zip](https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest/download/NexonicDownloadManager-Windows-Portable.zip) |
| Chrome, Edge, Brave, Opera extension | [NexonicExtension-Chrome.zip](https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest/download/NexonicExtension-Chrome.zip) |
| Firefox add-on | [NexonicAddon-Firefox.xpi](https://github.com/Shahrukhalid/nexonic-download-manager/releases/latest/download/NexonicAddon-Firefox.xpi) |

Everything is included. Nothing else to install.

## Install

**Mac:** open the `.dmg` and drag the app to **Applications**. The first time, macOS asks for permission:
**System Settings → Privacy & Security → Open Anyway** (on macOS 13–14: right-click the app → **Open**).

**Windows:** run the Setup file. If Windows says "Windows protected your PC", click **More info → Run anyway**.

**Browser** (open the app once first):

- **Chrome, Edge, Brave, Opera:** unzip the extension, open the browser's extensions page, turn on **Developer mode**,
  click **Load unpacked** and pick the folder.
- **Firefox:** drag the `.xpi` onto Firefox. Firefox only installs signed add-ons; for a quick test use
  `about:debugging` → **Load Temporary Add-on**.

Need help? Read the **[user guide](docs/HELP.md)** (also built into the app: Help menu on Mac, F1 on Windows).

## Features

- **Fast downloads** that split each file into parts and download them at the same time.
- **Pause and resume**, even after a restart. Expired links can be refreshed without starting over.
- **Videos from YouTube, Instagram, Facebook, X, TikTok and 1,000+ sites**, in the best quality (up to 4K/8K) or sound
  only.
- **Download this video** button on any web page, and streamed video saved as one `.mp4`.
- **Browser integration** for Chrome, Edge, Brave, Opera and Firefox.
- **Scheduler and queues:** start at night, stop in the morning, sleep or shut down when done.
- **Batch downloads** (`img[001-100].jpg`), a **site grabber**, and **clipboard** detection.
- **Speed limits**, proxy, password-protected downloads and checksum checks.
- **Same app on Mac and Windows**, light and dark mode, no account, no tracking.

<p align="center">
  <img src="https://raw.githubusercontent.com/Shahrukhalid/nexonic-download-manager/screenshots/mac-add.png" alt="Add URL" width="290">
  <img src="https://raw.githubusercontent.com/Shahrukhalid/nexonic-download-manager/screenshots/mac-scheduler.png" alt="Scheduler" width="290">
  <img src="https://raw.githubusercontent.com/Shahrukhalid/nexonic-download-manager/screenshots/windows-main.png" alt="Windows" width="290">
</p>

## How to use

| To | Do this |
|---|---|
| Download a link | **Add URL**, paste the link, **Start Download** |
| Save a YouTube / Instagram / Facebook / X / TikTok video | Point at the video → **Download this video**, or paste its link into **Add URL** |
| Download from a website | Click the download link in your browser; the app takes over |
| Download at night | **Scheduler** → **New Queue** → set the time → add downloads with **Download Later** |
| Continue an expired download | Right-click → **Refresh Link…** |

Command line: `ndm <link>` (Mac: **Options → Browsers & Command Line → Install ndm Command**; Windows: added by the
installer). Run `ndm help` for options.

Copy-protected services (Netflix, Disney+, Prime Video, Spotify) are not supported. Only download videos you have the
right to save.

## Build from source

```bash
git clone https://github.com/Shahrukhalid/nexonic-download-manager.git && cd nexonic-download-manager
UNIVERSAL=0 bash scripts/build-app.sh     # Mac
pwsh windows/build.ps1                    # Windows (.NET 8 SDK, Inno Setup 6)
```

## License

[MIT](LICENSE) © Shahrukh Khalid. Not affiliated with Internet Download Manager.
