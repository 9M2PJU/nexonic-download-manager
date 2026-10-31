import AppKit
import Foundation
import NexonicCore
import UserNotifications

@MainActor
final class DownloadManager: ObservableObject {
    @Published private(set) var items: [DownloadRecord] = []
    @Published private(set) var speeds: [UUID: Double] = [:]  // bytes per second
    @Published var settings: AppSettings {
        didSet {
            limiter.setLimit(bytesPerSecond: settings.speedLimitKBps * 1024)
            save(settings, to: AppPaths.settings)
            schedule()
        }
    }

    private var transfers: [UUID: Transfer] = [:]
    private var lastReceived: [UUID: Int64] = [:]
    private let limiter = SpeedLimiter()
    private var timer: Timer?
    private var ticks = 0

    init() {
        try? FileManager.default.createDirectory(at: AppPaths.support, withIntermediateDirectories: true)
        settings = Self.load(AppSettings.self, from: AppPaths.settings) ?? AppSettings()
        items = Self.load([DownloadRecord].self, from: AppPaths.downloads) ?? []
        for i in items.indices where items[i].status.isActive { items[i].status = .queued }  // interrupted by quit
        limiter.setLimit(bytesPerSecond: settings.speedLimitKBps * 1024)
        timer = Timer.scheduledTimer(withTimeInterval: 0.5, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.tick() }
        }
        if settings.notifications {
            UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound]) { _, _ in }
        }
        collectInbox()
        schedule()
    }

    // MARK: adding

    func add(links text: String, folder: URL? = nil) {
        for line in text.split(whereSeparator: \.isNewline) {
            let s = line.trimmingCharacters(in: .whitespaces)
            let job = DownloadJob(url: s)
            if job.validURL != nil { add(job, folder: folder) }
        }
    }

    func add(_ job: DownloadJob, folder: URL? = nil) {
        guard let url = job.validURL else { return }
        let record = DownloadRecord(url: url, folder: folder ?? settings.folder, fileName: job.fileName,
                                    referrer: job.referrer, cookies: job.cookies, userAgent: job.userAgent)
        items.append(record)
        schedule()
        persist()
    }

    func collectInbox() {
        let jobs = Inbox().collect()
        for job in jobs { add(job) }
        if !jobs.isEmpty { NSApp.activate(ignoringOtherApps: false) }
    }

    // MARK: control

    func pause(_ id: UUID) {
        if let t = transfers[id] { t.pause() } else { update(id) { $0.status = .paused } }
    }

    func resume(_ id: UUID) {
        update(id) { if [.paused, .failed].contains($0.status) { $0.status = .queued; $0.error = nil } }
        schedule()
    }

    func remove(_ id: UUID, deleteFile: Bool = false) {
        transfers.removeValue(forKey: id)?.discard()
        if deleteFile, let path = items.first(where: { $0.id == id })?.savedPath {
            try? FileManager.default.trashItem(at: URL(fileURLWithPath: path), resultingItemURL: nil)
        }
        try? FileManager.default.removeItem(at: AppPaths.parts.appendingPathComponent(id.uuidString))
        items.removeAll { $0.id == id }
        persist()
        schedule()
    }

    func pauseAll() { for i in items where i.status.isActive || i.status == .queued { pause(i.id) } }
    func resumeAll() { for i in items where i.status == .paused { resume(i.id) } }
    func clearFinished() { for i in items where i.status == .finished { remove(i.id) } }

    func reveal(_ id: UUID) {
        guard let path = items.first(where: { $0.id == id })?.savedPath else { return }
        NSWorkspace.shared.activateFileViewerSelecting([URL(fileURLWithPath: path)])
    }

    func open(_ id: UUID) {
        guard let path = items.first(where: { $0.id == id })?.savedPath else { return }
        NSWorkspace.shared.open(URL(fileURLWithPath: path))
    }

    // MARK: queue

    private func schedule() {
        let active = items.filter { $0.status.isActive }.count
        var slots = max(1, settings.maxActive) - active
        for item in items where item.status == .queued && slots > 0 {
            slots -= 1
            let t = Transfer(record: item, connections: settings.connections, limiter: limiter,
                             sortIntoCategories: settings.sortIntoCategoryFolders)
            t.onChange = { [weak self] rec in Task { @MainActor in self?.changed(rec) } }
            transfers[item.id] = t
            update(item.id) { $0.status = .probing }
            t.start()
        }
    }

    private func changed(_ rec: DownloadRecord) {
        guard let i = items.firstIndex(where: { $0.id == rec.id }) else { return }
        let before = items[i].status
        items[i] = rec
        if before != rec.status, [.finished, .failed, .paused].contains(rec.status) {
            transfers.removeValue(forKey: rec.id)
            if rec.status == .finished { notifyDone(rec) }
            if rec.status == .failed { notifyFailed(rec) }
            persist()
            schedule()
        }
    }

    private func update(_ id: UUID, _ change: (inout DownloadRecord) -> Void) {
        guard let i = items.firstIndex(where: { $0.id == id }) else { return }
        change(&items[i])
        persist()
    }

    private func tick() {
        ticks += 1
        var s: [UUID: Double] = [:]
        for item in items where item.status.isActive {
            let r = item.received
            s[item.id] = max(0, Double(r - (lastReceived[item.id] ?? r)) * 2)
            lastReceived[item.id] = r
        }
        speeds = s
        let active = items.filter { $0.status.isActive }.count
        NSApp.dockTile.badgeLabel = active > 0 ? "\(active)" : nil
        if ticks % 2 == 0 { collectInbox() }
        if ticks % 4 == 0 { persist() }
    }

    func eta(_ item: DownloadRecord) -> String {
        guard let size = item.size, let speed = speeds[item.id], speed > 0 else { return "" }
        let seconds = Double(size - item.received) / speed
        let f = DateComponentsFormatter()
        f.allowedUnits = seconds > 3600 ? [.hour, .minute] : [.minute, .second]
        f.unitsStyle = .abbreviated
        return f.string(from: seconds) ?? ""
    }

    // MARK: notifications and storage

    private func notifyDone(_ rec: DownloadRecord) {
        guard settings.notifications else { return }
        let c = UNMutableNotificationContent()
        c.title = "Download finished"
        c.body = rec.fileName
        UNUserNotificationCenter.current().add(UNNotificationRequest(identifier: rec.id.uuidString, content: c, trigger: nil))
    }

    private func notifyFailed(_ rec: DownloadRecord) {
        guard settings.notifications else { return }
        let c = UNMutableNotificationContent()
        c.title = "Download failed"
        c.body = "\(rec.fileName): \(rec.error ?? "")"
        UNUserNotificationCenter.current().add(UNNotificationRequest(identifier: rec.id.uuidString, content: c, trigger: nil))
    }

    private func persist() { save(items, to: AppPaths.downloads) }

    private func save<T: Encodable>(_ value: T, to url: URL) {
        guard let data = try? JSONEncoder().encode(value) else { return }
        try? data.write(to: url, options: .atomic)
    }

    private static func load<T: Decodable>(_ type: T.Type, from url: URL) -> T? {
        guard let data = try? Data(contentsOf: url) else { return nil }
        return try? JSONDecoder().decode(type, from: data)
    }
}
