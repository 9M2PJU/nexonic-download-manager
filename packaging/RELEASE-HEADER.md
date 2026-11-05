# Nexonic Download Manager 0.11.0

**New: torrents on Mac**, joining Windows and Linux, all with the same built-in engine (no extra install). Click **Torrents** in the toolbar, turn torrents on, then add a magnet, a torrent link or a `.torrent` file. Preview files, pick what to download, pause/resume, limit speeds and optionally seed. Every finished file is piece-checked and gets a SHA-256. Android and iPhone/iPad torrents are still in development. Android APK licensing and corresponding-source requirements remain unchanged; the release must include `Android-GPL-Sources.tar.gz`.

**Mac, first launch:** macOS says it *could not verify* the app because it's free and not sold through Apple. Click **Done**, open **System Settings → Privacy & Security**, click **Open Anyway**, then **Open Anyway** again and enter your Mac password. Only once; updates installed with **Update Now** open without a warning. [Pictures of each step](https://github.com/Shahrukhalid/nexonic-download-manager#first-launch-on-mac)

**Windows:** if SmartScreen appears, click **More info → Run anyway**.

**Android:** open `NexonicDownloadManager-Android.apk` on your phone and allow installing from your browser or Files app when asked. Older 32-bit phones: `NexonicDownloadManager-Android-32bit.apk`.

**Linux:** `NexonicDownloadManager-Linux-x86_64.AppImage` runs on every distribution (`chmod +x`, then open it); ARM computers: `-aarch64.AppImage`. Ubuntu, Debian, Mint, Pop!_OS: the `.deb`. Fedora, openSUSE: the `.rpm`. Check files with `sha256sum -c --ignore-missing SHA256SUMS-linux-x64.txt`. [Linux guide](https://github.com/Shahrukhalid/nexonic-download-manager/blob/main/docs/LINUX.md)

**Torrents:** proxies, the shared global speed limit and DNS over HTTPS are not used for torrents; peers and trackers can see your IP address. [Torrent guide](https://github.com/Shahrukhalid/nexonic-download-manager/blob/main/docs/TORRENTS.md)

