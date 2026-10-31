import AppKit
import NexonicCore
import SwiftUI
import UniformTypeIdentifiers

enum SidebarFilter: Hashable {
    case all, active, unfinished, finished, category(FileCategory), queue(String)

    func title(_ m: DownloadManager) -> String {
        switch self {
        case .all: return "All Downloads"
        case .active: return "In Progress"
        case .unfinished: return "Unfinished"
        case .finished: return "Finished"
        case .category(let c): return c.rawValue
        case .queue(let id): return m.queueName(id)
        }
    }

    func matches(_ r: DownloadRecord) -> Bool {
        switch self {
        case .all: return true
        case .active: return r.status.isActive || r.status == .queued
        case .unfinished: return r.status != .finished
        case .finished: return r.status == .finished
        case .category(let c): return r.category == c
        case .queue(let id): return r.queue == id
        }
    }
}

enum Sheet: Identifiable {
    case add(AddRequest), batch, scheduler(String), grabber, properties(UUID), refresh(UUID)

    var id: String {
        switch self {
        case .add(let r): return "add-\(r.id)"
        case .batch: return "batch"
        case .scheduler: return "scheduler"
        case .grabber: return "grabber"
        case .properties(let id): return "properties-\(id)"
        case .refresh(let id): return "refresh-\(id)"
        }
    }
}

let brand = Color(red: 0.0, green: 0.56, blue: 0.62)

struct ContentView: View {
    @EnvironmentObject var manager: DownloadManager
    @State private var filter: SidebarFilter = .all
    @State private var selection = Set<UUID>()
    @State private var sheet: Sheet?

    private var shown: [DownloadRecord] { Array(manager.items.filter(filter.matches).reversed()) }
    private var currentQueue: String {
        if case .queue(let id) = filter { return id }
        return DownloadQueue.mainID
    }

    var body: some View {
        NavigationSplitView {
            List(selection: $filter) {
                Section("Downloads") {
                    ForEach([SidebarFilter.all, .active, .unfinished, .finished], id: \.self) { f in
                        Label(f.title(manager), systemImage: icon(f)).tag(f)
                    }
                }
                Section("Categories") {
                    ForEach(FileCategory.allCases, id: \.self) { c in
                        Label(c.rawValue, systemImage: c.symbol).tag(SidebarFilter.category(c))
                    }
                }
                Section("Queues") {
                    ForEach(manager.queues) { q in
                        Label(q.name, systemImage: q.running ? "play.circle.fill" : "pause.circle").tag(SidebarFilter.queue(q.id))
                    }
                }
            }
            .navigationSplitViewColumnWidth(min: 180, ideal: 200)
        } detail: {
            VStack(spacing: 0) {
                table
                if selection.count == 1, let id = selection.first, let item = manager.items.first(where: { $0.id == id }) {
                    Divider()
                    DetailPane(item: item, bps: manager.speeds[id] ?? 0, eta: manager.eta(item), queue: manager.queueName(item.queue))
                }
            }
            .navigationTitle(filter.title(manager))
            .toolbar { toolbar }
        }
        .tint(brand)
        .sheet(item: $sheet) { s in sheetView(s).environmentObject(manager) }
        .onChange(of: manager.addRequest?.id) { _ in
            if let r = manager.addRequest { sheet = .add(r) }
        }
        .onOpenURL { _ in manager.collectInbox() }
        .onDrop(of: [.url, .plainText], isTargeted: nil) { providers in
            for p in providers {
                _ = p.loadObject(ofClass: URL.self) { url, _ in
                    if let url { Task { @MainActor in manager.addRequest = AddRequest(links: url.absoluteString) } }
                }
            }
            return true
        }
    }

    private var table: some View {
        Table(shown, selection: $selection) {
            TableColumn("Name") { r in Label(r.fileName, systemImage: r.category.symbol).lineLimit(1) }
            TableColumn("Size") { r in Text(r.size.map(bytes) ?? "–") }.width(80)
            TableColumn("Progress") { r in ProgressView(value: r.progress).progressViewStyle(.linear) }.width(110)
            TableColumn("Speed") { r in Text(r.status.isActive ? speed(manager.speeds[r.id] ?? 0) : "") }.width(90)
            TableColumn("Time Left") { r in Text(r.status.isActive ? manager.eta(r) : "") }.width(70)
            TableColumn("Status") { r in Text(statusText(r)).foregroundStyle(r.status == .failed ? .red : .secondary) }.width(110)
            TableColumn("Queue") { r in Text(manager.queueName(r.queue)).foregroundStyle(.secondary) }.width(90)
            TableColumn("Added") { r in Text(r.added, style: .date).foregroundStyle(.secondary) }.width(90)
        }
        .contextMenu(forSelectionType: UUID.self) { ids in
            rowMenu(ids)
        } primaryAction: { ids in
            if let id = ids.first { manager.open(id) }
        }
    }

    @ToolbarContentBuilder private var toolbar: some ToolbarContent {
        ToolbarItemGroup {
            Button { sheet = .add(AddRequest(links: clipboardLink(), queueID: currentQueue)) } label: {
                Label("Add URL", systemImage: "plus.circle")
            }
            .keyboardShortcut("n")
            .help("Add one or more links")
            Button { selection.forEach(manager.resume) } label: { Label("Resume", systemImage: "play.fill") }
                .disabled(selection.isEmpty).help("Resume the selected downloads")
            Button { selection.forEach(manager.pause) } label: { Label("Stop", systemImage: "pause.fill") }
                .disabled(selection.isEmpty).help("Stop the selected downloads")
            Button { manager.pauseAll() } label: { Label("Stop All", systemImage: "stop.fill") }.help("Stop every download")
            Button { selection.forEach { manager.remove($0) }; selection = [] } label: { Label("Delete", systemImage: "trash") }
                .disabled(selection.isEmpty).help("Remove the selected downloads from the list")
            Button { manager.clearFinished() } label: { Label("Delete Completed", systemImage: "checkmark.circle") }
                .help("Remove finished downloads from the list")
        }
        ToolbarItemGroup {
            Button { sheet = .scheduler(currentQueue) } label: { Label("Scheduler", systemImage: "calendar.badge.clock") }
                .help("Queues and timetables")
            Button { manager.startQueue(currentQueue) } label: { Label("Start Queue", systemImage: "play.circle") }
                .help("Start \(manager.queueName(currentQueue))")
            Button { manager.stopQueue(currentQueue) } label: { Label("Stop Queue", systemImage: "stop.circle") }
                .help("Stop \(manager.queueName(currentQueue))")
            Button { sheet = .grabber } label: { Label("Grabber", systemImage: "globe") }.help("Download files from a website section")
            Button { sheet = .batch } label: { Label("Batch", systemImage: "number") }.help("Download a numbered series of files")
            if #available(macOS 14, *) {
                SettingsLink { Label("Options", systemImage: "gearshape") }.help("Options")
            } else {
                Button { NSApp.sendAction(Selector(("showSettingsWindow:")), to: nil, from: nil) } label: {
                    Label("Options", systemImage: "gearshape")
                }
            }
        }
    }

    @ViewBuilder private func sheetView(_ s: Sheet) -> some View {
        switch s {
        case .add(let r): AddSheet(request: r, onClose: closeAdd)
        case .batch: BatchSheet(queueID: currentQueue)
        case .scheduler(let q): SchedulerSheet(selected: q)
        case .grabber: GrabberSheet(queueID: currentQueue)
        case .properties(let id): PropertiesSheet(id: id)
        case .refresh(let id): RefreshSheet(id: id)
        }
    }

    private func closeAdd() {
        manager.addRequest = nil
        sheet = nil
    }

    @ViewBuilder private func rowMenu(_ ids: Set<UUID>) -> some View {
        if ids.count == 1, let id = ids.first {
            Button("Open") { manager.open(id) }
            Button("Show in Finder") { manager.reveal(id) }
            Divider()
        }
        Button("Resume") { ids.forEach(manager.resume) }
        Button("Stop") { ids.forEach(manager.pause) }
        if ids.count == 1, let id = ids.first {
            Button("Refresh Link…") { sheet = .refresh(id) }
            Button("Properties…") { sheet = .properties(id) }
            Button("Copy Link") {
                if let u = manager.items.first(where: { $0.id == id })?.url {
                    NSPasteboard.general.clearContents()
                    NSPasteboard.general.setString(u.absoluteString, forType: .string)
                }
            }
        }
        Menu("Move to Queue") {
            ForEach(manager.queues) { q in
                Button(q.name) { moveTo(ids, q.id) }
            }
        }
        Divider()
        Button("Delete") { ids.forEach { manager.remove($0) }; selection.subtract(ids) }
        Button("Delete and Move File to Trash") { ids.forEach { manager.remove($0, deleteFile: true) }; selection.subtract(ids) }
    }

    private func moveTo(_ ids: Set<UUID>, _ queue: String) {
        for id in ids {
            guard let r = manager.items.first(where: { $0.id == id }) else { continue }
            manager.setDetails(id, username: r.username ?? "", password: r.password ?? "", checksum: r.checksum ?? "",
                               speedLimitKBps: r.speedLimitKBps ?? 0, queueID: queue)
        }
    }

    private func icon(_ f: SidebarFilter) -> String {
        switch f {
        case .all: return "tray.full"
        case .active: return "arrow.down.circle"
        case .unfinished: return "clock"
        case .finished: return "checkmark.circle"
        case .category(let c): return c.symbol
        case .queue: return "list.bullet"
        }
    }
}

func clipboardLink() -> String {
    guard let s = NSPasteboard.general.string(forType: .string)?.trimmingCharacters(in: .whitespacesAndNewlines),
          DownloadJob(url: s).validURL != nil else { return "" }
    return s
}

func bytes(_ n: Int64) -> String { ByteCountFormatter.string(fromByteCount: n, countStyle: .file) }
func speed(_ bps: Double) -> String { bps > 0 ? "\(bytes(Int64(bps)))/s" : "" }

func statusText(_ r: DownloadRecord) -> String {
    switch r.status {
    case .queued: return "Queued"
    case .probing: return "Connecting…"
    case .downloading: return "\(Int(r.progress * 100))%"
    case .paused: return r.awaitingRefresh == true ? "Waiting for link" : "Stopped"
    case .finished: return r.checksumResult.map { $0.contains("not") ? "Checksum failed" : "Complete" } ?? "Complete"
    case .failed: return "Error"
    }
}

struct DetailPane: View {
    let item: DownloadRecord
    let bps: Double
    let eta: String
    let queue: String

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(item.fileName).font(.headline).lineLimit(1)
            Text(item.url.absoluteString).font(.caption).foregroundStyle(.secondary).lineLimit(1).textSelection(.enabled)
            SegmentBar(segments: item.segments, size: item.size).frame(height: 14)
            HStack(spacing: 16) {
                Text("\(bytes(item.received)) of \(item.size.map(bytes) ?? "unknown size")")
                if item.status.isActive {
                    Text(speed(bps))
                    if !eta.isEmpty { Text("\(eta) left") }
                    Text("Connections: \(item.segments.filter { !$0.isComplete }.count)")
                }
                Text(item.acceptsRanges || item.status == .queued ? "Resume: yes" : "Resume: no")
                Text("Queue: \(queue)")
                if let l = item.speedLimitKBps { Text("Limit: \(l) KB/s") }
            }
            .font(.caption)
            if let c = item.checksumResult { Text(c).font(.caption).foregroundStyle(c.contains("not") ? .red : .green) }
            if let e = item.error { Text(e).font(.caption).foregroundStyle(.red).lineLimit(2) }
        }
        .padding(12)
        .frame(maxWidth: .infinity, alignment: .leading)
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
                         with: .color(s.isComplete ? .green : brand))
            }
        }
    }
}
