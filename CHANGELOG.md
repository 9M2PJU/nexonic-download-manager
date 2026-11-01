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
