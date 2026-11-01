## 0.4.3

- **Help:** a built-in Help window on Mac (Help menu, ⌘?) and Windows (Help button, F1) with topics for getting started,
  the browser extension, videos, queues, batch and grabber, settings, the command line, troubleshooting and shortcuts.
  The same guide is online in docs/HELP.md.
- **About:** a new About window with the version, "Made by Shahrukh Khalid", links to the website, GitHub, source code
  and problem reports, the license and a Check for Updates button.
- Help menu on Mac: Online Help, Report a Problem, Release Notes, Website and Source Code.

## 0.4.2

- New app icon on Mac, Windows and in the browser extension.
- Mac installer window: clear "drag to Applications" layout, the browser add-ons below, and a thank-you note with links
  to github.com/Shahrukhalid and www.shahrukhalid.com.
- Windows installer: the same thank-you note and links on the last page.

## 0.4.1

- Video sites: the progress bar, speed and time left now update while a video downloads (before, it jumped from 0% to
  done), and continue smoothly from the picture to the sound part.
- Cleaner file names for saved videos (the page title only).

## 0.4.0

- **YouTube, Instagram, Facebook, X (Twitter), TikTok, Vimeo, Reddit and 1,000+ other sites:** save videos in the best
  quality (up to 4K/8K) or sound only, from the video button, the extension popup, the right-click menu, Add URL or `ndm`.
  Feeds work too: the button saves the post a video belongs to.
- Built-in video engine that keeps itself up to date, and it now also joins WebM/VP9/AV1 and Opus. Nothing else to
  install.
- Copy-protected services stay unsupported.

## 0.3.0

- **Streamed video (HLS):** the extension now finds streamed videos on any site that doesn't copy-protect them, lists
  them best quality first (for example 1080p) with the page's title as the name, and hides the lower qualities.
- The app downloads the highest resolution and bit rate, fetches the pieces in parallel, resumes after a pause or
  restart, and joins picture and sound into one `.mp4`.
- Everything is included, so streamed video always comes out as one `.mp4` with no extra installs.
- Copy-protected videos are shown as protected instead of failing later.
- README shows real screenshots of the Mac and Windows apps, taken automatically from each build.
- Paste a `.m3u8` link into Add URL, or use `ndm https://…/master.m3u8`.

## 0.2.1

- Cleaner, more professional windows on Mac and Windows: the system's file icons in the list, coloured status dots,
  item counts in the sidebar, a search box, a status bar with totals and speed, and a helpful empty view.
- Release files now keep the same names, so the download links in the README always fetch the newest version.
- README: direct downloads and step-by-step setup for Mac, Windows, Chrome-type browsers and Firefox.

## 0.2.0

The Mac and Windows apps now have the same features and toolbar, and there is a Firefox add-on.

- **Scheduler and queues:** several queues, each with start and stop times, weekdays, its own limit and an action when
  done (quit, sleep or shut down). Move downloads between queues.
- **Batch downloads** from patterns such as `file[001-100].jpg`, `[a-z]` and `[0-100:5]`.
- **Site Grabber:** explore a website section and pick its files by type.
- **Clipboard monitoring:** copying a link to a file opens the Add window.
- **Download this video** button over players, plus media and link lists in the extension popup.
- **Proxy** (system, HTTP, SOCKS5) and **password-protected downloads**.
- **Refresh Link:** continue an expired download from a new link, pasted or caught from the browser.
- **After a download:** Download Complete window, sound, checksum check, optional virus scan, files marked as downloaded.
- **Per-download speed limit** and a richer Add window (name, folder, queue, login, checksum, limit, Download Later).
- **Command line:** `ndm` to add links, batches, queues and options from scripts.
- **Firefox add-on** (`.xpi`) next to the Chrome extension.

## 0.1.0

First release.

- Downloads split into up to 32 parallel connections; a free connection takes over half of the largest remaining part.
- Pause, resume after quitting, automatic retries, speed limit and a download queue.
- Categories, a per-part progress bar, notifications, a menu bar icon and a Dock badge.
- Windows app with the same engine: installer and portable zip, tray icon, single instance.
- Browser extension: catches downloads (with the page's sign-in cookies), right-click "Download with Nexonic", popup with settings.
