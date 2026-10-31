import AppKit
import NexonicCore
import SwiftUI

struct SettingsView: View {
    @EnvironmentObject var manager: DownloadManager
    @State private var message = ""

    var body: some View {
        TabView {
            Form {
                Stepper("Connections per download: \(manager.settings.connections)",
                        value: $manager.settings.connections, in: 1...32)
                Stepper("Downloads at the same time: \(manager.settings.maxActive)",
                        value: $manager.settings.maxActive, in: 1...10)
                HStack {
                    Text("Speed limit (KB/s, 0 = none)")
                    TextField("", value: $manager.settings.speedLimitKBps, format: .number).frame(width: 90)
                }
                HStack {
                    Text("Save to: \(manager.settings.folder.path)").lineLimit(1)
                    Spacer()
                    Button("Choose…") { chooseFolder() }
                }
                Toggle("Sort into category folders", isOn: $manager.settings.sortIntoCategoryFolders)
                Toggle("Notifications", isOn: $manager.settings.notifications)
            }
            .padding(20)
            .tabItem { Label("General", systemImage: "gearshape") }

            VStack(alignment: .leading, spacing: 12) {
                Text("Browser extension").font(.headline)
                Text("""
                1. Click Save Extension Folder and pick a place (for example Documents).
                2. In your browser open the extensions page, switch on Developer mode and choose Load unpacked.
                3. Select the saved Nexonic Extension folder. Downloads from the browser now open here.
                """).fixedSize(horizontal: false, vertical: true)
                Button("Save Extension Folder…") { saveExtension() }
                if !message.isEmpty { Text(message).font(.caption).foregroundStyle(.secondary) }
            }
            .padding(20)
            .tabItem { Label("Browser", systemImage: "globe") }
        }
        .frame(width: 520, height: 300)
    }

    private func chooseFolder() {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        if panel.runModal() == .OK, let url = panel.url { manager.settings.folder = url }
    }

    private func saveExtension() {
        let panel = NSOpenPanel()
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.prompt = "Save Here"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            let dest = try HostInstaller.saveExtension(to: url)
            message = "Saved to \(dest.path)"
            NSWorkspace.shared.activateFileViewerSelecting([dest])
        } catch {
            message = "Could not save the extension: \(error.localizedDescription)"
        }
    }
}
