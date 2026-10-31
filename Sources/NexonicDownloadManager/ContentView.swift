import AppKit
import NexonicCore
import SwiftUI
import UniformTypeIdentifiers

enum SidebarFilter: Hashable {
    case all, active, unfinished, finished, category(FileCategory)

    var title: String {
        switch self {
        case .all: return "All Downloads"
        case .active: return "In Progress"
        case .unfinished: return "Unfinished"
        case .finished: return "Finished"
        case .category(let c): return c.rawValue
        }
    }

    func matches(_ r: DownloadRecord) -> Bool {
        switch self {
        case .all: return true
        case .active: return r.status.isActive || r.status == .queued
        case .unfinished: return r.status != .finished
        case .finished: return r.status == .finished
        case .category(let c): return r.category == c
        }
    }
}

struct ContentView: View {
    @EnvironmentObject var manager: DownloadManager
    @State private var filter: SidebarFilter = .all
    @State private var selection: UUID?
    @State private var adding = false

    private var shown: [DownloadRecord] { Array(manager.items.filter(filter.matches).reversed()) }

    var body: some View {
        NavigationSplitView {
            List(selection: $filter) {
                Section("Downloads") {
                    ForEach([SidebarFilter.all, .active, .unfinished, .finished], id: \.self) { f in
                        Label(f.title, systemImage: icon(f)).tag(f)
                    }
                }
                Section("Categories") {
                    ForEach(FileCategory.allCases, id: \.self) { c in
                        Label(c.rawValue, systemImage: c.symbol).tag(SidebarFilter.category(c))
                    }
                }
            }
            .navigationSplitViewColumnWidth(min: 170, ideal: 190)
        } detail: {
            VStack(spacing: 0) {
                Table(shown, selection: $selection) {
                    TableColumn("Name") { r in Label(r.fileName, systemImage: r.category.symbol).lineLimit(1) }
                    TableColumn("Size") { r in Text(r.size.map(bytes) ?? "–") }.width(80)
                    TableColumn("Progress") { r in ProgressView(value: r.progress).progressViewStyle(.linear) }.width(110)
                    TableColumn("Speed") { r in Text(r.status.isActive ? speed(manager.speeds[r.id] ?? 0) : "") }.width(90)
                    TableColumn("Time Left") { r in Text(r.status.isActive ? manager.eta(r) : "") }.width(70)
                    TableColumn("Status") { r in Text(statusText(r)).foregroundStyle(r.status == .failed ? .red : .secondary) }.width(100)
                }
                .contextMenu(forSelectionType: UUID.self) { ids in
                    if let id = ids.first { rowMenu(id) }
                } primaryAction: { ids in
                    if let id = ids.first { manager.open(id) }
                }
                if let id = selection, let item = manager.items.first(where: { $0.id == id }) {
                    Divider()
                    DetailPane(item: item, speed: manager.speeds[id] ?? 0)
                }
            }
            .navigationTitle(filter.title)
            .toolbar {
                ToolbarItemGroup {
                    Button { adding = true } label: { Label("Add", systemImage: "plus") }
                        .keyboardShortcut("n")
                    Button { if let id = selection { manager.resume(id) } } label: { Label("Resume", systemImage: "play.fill") }
                    Button { if let id = selection { manager.pause(id) } } label: { Label("Pause", systemImage: "pause.fill") }
                    Button { if let id = selection { manager.remove(id); selection = nil } } label: { Label("Remove", systemImage: "trash") }
                    Button { manager.clearFinished() } label: { Label("Clear Finished", systemImage: "checkmark.circle") }
                }
            }
        }
        .sheet(isPresented: $adding) { AddSheet().environmentObject(manager) }
        .onDrop(of: [.url, .plainText], isTargeted: nil) { providers in
            for p in providers {
                _ = p.loadObject(ofClass: URL.self) { url, _ in
                    if let url { Task { @MainActor in manager.add(links: url.absoluteString) } }
                }
            }
            return true
        }
    }

    @ViewBuilder private func rowMenu(_ id: UUID) -> some View {
        Button("Open") { manager.open(id) }
        Button("Show in Finder") { manager.reveal(id) }
        Divider()
        Button("Resume") { manager.resume(id) }
        Button("Pause") { manager.pause(id) }
        Button("Copy Link") {
            if let u = manager.items.first(where: { $0.id == id })?.url {
                NSPasteboard.general.clearContents()
                NSPasteboard.general.setString(u.absoluteString, forType: .string)
            }
        }
        Divider()
        Button("Remove") { manager.remove(id) }
        Button("Remove and Move File to Trash") { manager.remove(id, deleteFile: true) }
    }

    private func icon(_ f: SidebarFilter) -> String {
        switch f {
        case .all: return "tray.full"
        case .active: return "arrow.down.circle"
        case .unfinished: return "clock"
        case .finished: return "checkmark.circle"
        case .category(let c): return c.symbol
        }
    }
}

func bytes(_ n: Int64) -> String { ByteCountFormatter.string(fromByteCount: n, countStyle: .file) }
func speed(_ bps: Double) -> String { bps > 0 ? "\(bytes(Int64(bps)))/s" : "" }

func statusText(_ r: DownloadRecord) -> String {
    switch r.status {
    case .queued: return "Queued"
    case .probing: return "Connecting…"
    case .downloading: return "\(Int(r.progress * 100))%"
    case .paused: return "Paused"
    case .finished: return "Finished"
    case .failed: return "Failed"
    }
}

struct DetailPane: View {
    let item: DownloadRecord
    let speed: Double

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(item.fileName).font(.headline).lineLimit(1)
            Text(item.url.absoluteString).font(.caption).foregroundStyle(.secondary).lineLimit(1).textSelection(.enabled)
            SegmentBar(segments: item.segments, size: item.size)
                .frame(height: 14)
            HStack(spacing: 16) {
                Text("\(bytes(item.received)) of \(item.size.map(bytes) ?? "unknown size")")
                Text("Connections: \(item.segments.filter { !$0.isComplete }.count)")
                if !item.acceptsRanges && item.status != .finished { Text("Server allows one connection only") }
                if let e = item.error { Text(e).foregroundStyle(.red).lineLimit(1) }
            }
            .font(.caption)
        }
        .padding(12)
    }
}

/// One bar for the whole file; each range shows how much of it has arrived.
struct SegmentBar: View {
    let segments: [Segment]
    let size: Int64?

    var body: some View {
        Canvas { ctx, sz in
            ctx.fill(Path(roundedRect: CGRect(origin: .zero, size: sz), cornerRadius: 3), with: .color(.secondary.opacity(0.2)))
            guard let size, size > 0 else { return }
            for s in segments {
                let x = sz.width * CGFloat(s.start) / CGFloat(size)
                let w = sz.width * CGFloat(s.written) / CGFloat(size)
                ctx.fill(Path(CGRect(x: x, y: 0, width: max(w, 0.5), height: sz.height)),
                         with: .color(s.isComplete ? .green : .accentColor))
            }
        }
    }
}

struct AddSheet: View {
    @EnvironmentObject var manager: DownloadManager
    @Environment(\.dismiss) private var dismiss
    @State private var text = ""

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            Text("Add Downloads").font(.title2.bold())
            Text("Paste one or more links, one per line.").foregroundStyle(.secondary)
            TextEditor(text: $text).font(.body.monospaced()).frame(minHeight: 140)
                .overlay(RoundedRectangle(cornerRadius: 6).stroke(.quaternary))
            HStack {
                Text("Saved to \(manager.settings.folder.path)").font(.caption).foregroundStyle(.secondary).lineLimit(1)
                Spacer()
                Button("Cancel") { dismiss() }.keyboardShortcut(.cancelAction)
                Button("Download") {
                    manager.add(links: text)
                    dismiss()
                }
                .keyboardShortcut(.defaultAction)
                .disabled(text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
            }
        }
        .padding(20)
        .frame(width: 520)
        .onAppear {
            if let s = NSPasteboard.general.string(forType: .string), s.hasPrefix("http") { text = s }
        }
    }
}
