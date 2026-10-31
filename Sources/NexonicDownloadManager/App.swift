import AppKit
import NexonicCore
import SwiftUI

@main
struct NexonicDownloadManagerApp: App {
    @StateObject private var manager = DownloadManager()

    init() { HostInstaller.install() }

    var body: some Scene {
        WindowGroup("Nexonic Download Manager", id: "main") {
            ContentView()
                .environmentObject(manager)
                .frame(minWidth: 820, minHeight: 460)
                .onOpenURL { _ in manager.collectInbox() }  // nexonicdm://inbox from the browser helper
        }
        .commands {
            CommandGroup(after: .newItem) {
                Button("Pause All") { manager.pauseAll() }.keyboardShortcut("p", modifiers: [.command, .shift])
                Button("Resume All") { manager.resumeAll() }.keyboardShortcut("r", modifiers: [.command, .shift])
            }
        }
        Settings {
            SettingsView().environmentObject(manager)
        }
        MenuBarExtra("Nexonic", systemImage: "arrow.down.circle") {
            MenuBarView().environmentObject(manager)
        }
    }
}

struct MenuBarView: View {
    @EnvironmentObject var manager: DownloadManager
    @Environment(\.openWindow) private var openWindow

    var body: some View {
        let active = manager.items.filter { $0.status.isActive }
        if active.isEmpty { Text("No active downloads") }
        ForEach(active.prefix(8)) { item in
            Text("\(item.fileName) – \(Int(item.progress * 100))%")
        }
        Divider()
        Button("Show Downloads") {
            openWindow(id: "main")
            NSApp.activate(ignoringOtherApps: true)
        }
        Button("Pause All") { manager.pauseAll() }
        Button("Resume All") { manager.resumeAll() }
        Divider()
        Button("Quit") { NSApp.terminate(nil) }
    }
}

/// Registers the browser helper so the extension can reach the app (only for browsers that are installed).
enum HostInstaller {
    static let browsers = [
        "Google/Chrome", "Google/Chrome Beta", "Google/Chrome Dev", "Google/Chrome Canary", "Chromium",
        "BraveSoftware/Brave-Browser", "Microsoft Edge", "Vivaldi",
    ]

    static func install() {
        let host = Bundle.main.bundleURL.appendingPathComponent("Contents/MacOS/nexonic-host")
        guard FileManager.default.isExecutableFile(atPath: host.path),
              let data = try? JSONSerialization.data(withJSONObject: NativeMessaging.manifest(hostPath: host.path),
                                                     options: [.prettyPrinted, .sortedKeys]) else { return }
        let support = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        for browser in browsers {
            let base = support.appendingPathComponent(browser)
            guard FileManager.default.fileExists(atPath: base.path) else { continue }
            let dir = base.appendingPathComponent("NativeMessagingHosts")
            try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
            try? data.write(to: dir.appendingPathComponent("\(NativeMessaging.hostName).json"), options: .atomic)
        }
    }

    /// Copies the bundled extension folder somewhere the user can load it from.
    static func saveExtension(to folder: URL) throws -> URL {
        guard let src = Bundle.main.url(forResource: "Extension", withExtension: nil) else {
            throw CocoaError(.fileNoSuchFile)
        }
        let dest = FileNames.unique("Nexonic Extension", in: folder)
        try FileManager.default.copyItem(at: src, to: dest)
        return dest
    }
}
