import Foundation

/// One download: probes the server, splits the file into byte ranges, downloads them in parallel into part files,
/// splits the largest remaining range whenever a connection frees up, retries failed ranges and joins the parts.
/// All state changes happen on `queue`; `onChange` is called with a copy of the record after each change.
public final class Transfer: NSObject, URLSessionDataDelegate, @unchecked Sendable {
    public private(set) var record: DownloadRecord
    public var onChange: (@Sendable (DownloadRecord) -> Void)?

    private let connections: Int
    private let limiter: SpeedLimiter
    private let sortIntoCategories: Bool
    private let queue: DispatchQueue
    private var session: URLSession!
    private var running: [Int: Running] = [:]  // by task identifier
    private var attempts: [Int: Int] = [:]  // by segment id
    private var stopping = false
    private var probeTask: Int?
    private var lastNotify = Date.distantPast

    private struct Running {
        let task: URLSessionDataTask
        let segmentID: Int
        let handle: FileHandle
    }

    private var partsFolder: URL { AppPaths.parts.appendingPathComponent(record.id.uuidString, isDirectory: true) }

    public init(record: DownloadRecord, connections: Int, limiter: SpeedLimiter, sortIntoCategories: Bool = false) {
        self.record = record
        self.connections = max(1, min(32, connections))
        self.limiter = limiter
        self.sortIntoCategories = sortIntoCategories
        queue = DispatchQueue(label: "nexonic.transfer.\(record.id.uuidString)")
        super.init()
        let config = URLSessionConfiguration.default
        config.httpMaximumConnectionsPerHost = 32
        config.timeoutIntervalForRequest = 30
        config.httpShouldSetCookies = false
        config.httpCookieAcceptPolicy = .never
        config.requestCachePolicy = .reloadIgnoringLocalCacheData
        let delegateQueue = OperationQueue()
        delegateQueue.maxConcurrentOperationCount = 1
        delegateQueue.underlyingQueue = queue
        session = URLSession(configuration: config, delegate: self, delegateQueue: delegateQueue)
    }

    // MARK: control

    public func start() {
        queue.async { self.begin() }
    }

    public func pause() {
        queue.async {
            guard self.record.status.isActive || self.record.status == .queued else { return }
            self.stopping = true
            self.record.status = .paused
            self.cancelAll()
            self.notify(force: true)
        }
    }

    /// Stops and removes part files. The finished file (if any) is kept.
    public func discard() {
        queue.async {
            self.stopping = true
            self.cancelAll()
            self.session.invalidateAndCancel()
            try? FileManager.default.removeItem(at: self.partsFolder)
        }
    }

    // MARK: start / resume

    private func request(range: String?) -> URLRequest {
        var req = URLRequest(url: record.url)
        req.setValue("identity", forHTTPHeaderField: "Accept-Encoding")
        if let range { req.setValue(range, forHTTPHeaderField: "Range") }
        if let ref = record.referrer, !ref.isEmpty { req.setValue(ref, forHTTPHeaderField: "Referer") }
        if let ua = record.userAgent, !ua.isEmpty { req.setValue(ua, forHTTPHeaderField: "User-Agent") }
        if let cookies = record.cookies, !cookies.isEmpty { req.setValue(cookies, forHTTPHeaderField: "Cookie") }
        return req
    }

    private func begin() {
        stopping = false
        record.status = .probing
        record.error = nil
        notify(force: true)
        // The probe is stopped as soon as the headers arrive, so a server that ignores ranges does not send the file.
        let task = session.dataTask(with: request(range: "bytes=0-0"))
        probeTask = task.taskIdentifier
        task.resume()
    }

    private func probed(_ response: HTTPURLResponse?, error: Error?) {
        guard !stopping else { return }
        guard let response, error == nil else {
            return fail(error?.localizedDescription ?? "No response from the server")
        }
        guard (200..<300).contains(response.statusCode) else { return fail("The server answered HTTP \(response.statusCode)") }
        var size: Int64?
        var ranges = false
        if response.statusCode == 206, let cr = response.value(forHTTPHeaderField: "Content-Range"),
           let total = cr.split(separator: "/").last, let n = Int64(total) {
            size = n
            ranges = true
        } else if response.expectedContentLength > 0 {
            size = response.expectedContentLength
        }
        if let name = FileNames.fromContentDisposition(response.value(forHTTPHeaderField: "Content-Disposition")),
           record.fileName == FileNames.fromURL(record.url) {
            record.fileName = name
        }
        let canResume = !record.segments.isEmpty && ranges && record.acceptsRanges && size != nil && size == record.size
        if canResume {
            for i in record.segments.indices {
                let file = partURL(record.segments[i].id)
                let onDisk = (try? FileManager.default.attributesOfItem(atPath: file.path)[.size] as? Int64) ?? 0
                let len = record.segments[i].length ?? onDisk
                record.segments[i].written = min(onDisk, len)
                truncate(file, to: record.segments[i].written)
            }
        } else {
            try? FileManager.default.removeItem(at: partsFolder)
            record.size = size
            record.acceptsRanges = ranges
            record.segments = SegmentPlanner.plan(size: ranges ? size : nil, connections: ranges ? connections : 1)
            if !ranges, let size { record.segments = [Segment(id: 0, start: 0, end: size - 1)] }
        }
        try? FileManager.default.createDirectory(at: partsFolder, withIntermediateDirectories: true)
        record.status = .downloading
        notify(force: true)
        for seg in record.segments where !seg.isComplete { launch(seg.id) }
        finishIfDone()
    }

    private func partURL(_ id: Int) -> URL { partsFolder.appendingPathComponent("\(id).part") }

    private func truncate(_ url: URL, to length: Int64) {
        if !FileManager.default.fileExists(atPath: url.path) { FileManager.default.createFile(atPath: url.path, contents: nil) }
        if let h = try? FileHandle(forWritingTo: url) {
            try? h.truncate(atOffset: UInt64(length))
            try? h.close()
        }
    }

    private func launch(_ segmentID: Int) {
        guard !stopping, let i = record.segments.firstIndex(where: { $0.id == segmentID }) else { return }
        let seg = record.segments[i]
        let file = partURL(seg.id)
        if !record.acceptsRanges { record.segments[i].written = 0 }
        truncate(file, to: record.segments[i].written)
        guard let handle = try? FileHandle(forWritingTo: file) else { return fail("Cannot write to \(file.path)") }
        _ = try? handle.seekToEnd()
        var range: String?
        if record.acceptsRanges {
            let from = seg.start + record.segments[i].written
            range = seg.end >= 0 ? "bytes=\(from)-\(seg.end)" : "bytes=\(from)-"
        }
        let task = session.dataTask(with: request(range: range))
        running[task.taskIdentifier] = Running(task: task, segmentID: seg.id, handle: handle)
        task.resume()
    }

    private func cancelAll() {
        for r in running.values {
            r.task.cancel()
            try? r.handle.close()
        }
        running.removeAll()
    }

    // MARK: URLSession delegate (runs on `queue`)

    public func urlSession(_ session: URLSession, task: URLSessionTask, willPerformHTTPRedirection response: HTTPURLResponse,
                           newRequest request: URLRequest, completionHandler: @escaping (URLRequest?) -> Void) {
        var next = request
        if next.url?.host?.lowercased() != record.url.host?.lowercased() {
            next.setValue(nil, forHTTPHeaderField: "Cookie")  // cookies only go to the original host
        }
        completionHandler(next)
    }

    public func urlSession(_ session: URLSession, dataTask: URLSessionDataTask, didReceive response: URLResponse,
                           completionHandler: @escaping (URLSession.ResponseDisposition) -> Void) {
        if dataTask.taskIdentifier == probeTask {
            probeTask = nil
            completionHandler(.cancel)
            probed(response as? HTTPURLResponse, error: nil)
            return
        }
        let code = (response as? HTTPURLResponse)?.statusCode ?? 0
        let wantsRange = record.acceptsRanges
        if code == 206 || (code == 200 && !wantsRange) {
            completionHandler(.allow)
        } else {
            completionHandler(.cancel)
        }
    }

    public func urlSession(_ session: URLSession, dataTask: URLSessionDataTask, didReceive data: Data) {
        guard let run = running[dataTask.taskIdentifier], let i = record.segments.firstIndex(where: { $0.id == run.segmentID }) else {
            return
        }
        var chunk = data
        let left = record.segments[i].remaining
        if Int64(chunk.count) > left { chunk = chunk.prefix(Int(left)) }
        if !chunk.isEmpty {
            do { try run.handle.write(contentsOf: chunk) } catch {
                return fail("Writing failed: \(error.localizedDescription)")
            }
            record.segments[i].written += Int64(chunk.count)
        }
        if record.segments[i].isComplete {
            dataTask.cancel()  // the range may have been shortened by a split
        } else {
            let wait = limiter.consume(chunk.count)
            if wait > 0 {
                dataTask.suspend()
                queue.asyncAfter(deadline: .now() + wait) { [weak dataTask] in dataTask?.resume() }
            }
        }
        notify()
    }

    public func urlSession(_ session: URLSession, task: URLSessionTask, didCompleteWithError error: Error?) {
        if task.taskIdentifier == probeTask {
            probeTask = nil
            if !stopping { probed(nil, error: error) }
            return
        }
        guard let run = running.removeValue(forKey: task.taskIdentifier) else { return }
        try? run.handle.close()
        guard !stopping, let i = record.segments.firstIndex(where: { $0.id == run.segmentID }) else { return }
        var seg = record.segments[i]
        if seg.end < 0, error == nil {  // unknown size: the stream ended normally
            seg.end = seg.start + seg.written - 1
            record.segments[i] = seg
            record.size = seg.written
        }
        if record.segments[i].isComplete {
            splitAndLaunch()
            finishIfDone()
            return
        }
        let n = (attempts[seg.id] ?? 0) + 1
        attempts[seg.id] = n
        guard n <= 6 else {
            return fail(error?.localizedDescription ?? "A part of the file could not be downloaded")
        }
        let delay = min(30, pow(2, Double(n - 1)))
        queue.asyncAfter(deadline: .now() + delay) { [weak self] in self?.launch(seg.id) }
    }

    private func splitAndLaunch() {
        guard record.acceptsRanges, running.count < connections else { return }
        let nextID = (record.segments.map(\.id).max() ?? 0) + 1
        guard let split = SegmentPlanner.splitLargest(record.segments, nextID: nextID) else { return }
        record.segments[split.index].end = split.newEnd
        record.segments.append(split.added)
        launch(split.added.id)
    }

    // MARK: finish

    private func finishIfDone() {
        guard !stopping, running.isEmpty, record.status == .downloading,
              !record.segments.isEmpty, record.segments.allSatisfy(\.isComplete) else { return }
        do {
            var folder = record.folder
            if sortIntoCategories { folder.appendPathComponent(record.category.rawValue, isDirectory: true) }
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            let target = FileNames.unique(record.fileName, in: folder)
            FileManager.default.createFile(atPath: target.path, contents: nil)
            let out = try FileHandle(forWritingTo: target)
            defer { try? out.close() }
            for seg in record.segments.sorted(by: { $0.start < $1.start }) {
                let input = try FileHandle(forReadingFrom: partURL(seg.id))
                while let block = try input.read(upToCount: 4 << 20), !block.isEmpty { try out.write(contentsOf: block) }
                try input.close()
            }
            let written = try out.offset()
            if let size = record.size, Int64(written) != size {
                return fail("Size check failed: expected \(size) bytes, got \(written)")
            }
            try? FileManager.default.removeItem(at: partsFolder)
            record.savedPath = target.path
            record.fileName = target.lastPathComponent
            record.status = .finished
            record.finished = Date()
            notify(force: true)
            session.finishTasksAndInvalidate()
        } catch {
            fail("Joining the parts failed: \(error.localizedDescription)")
        }
    }

    private func fail(_ message: String) {
        stopping = true
        cancelAll()
        record.status = .failed
        record.error = message
        notify(force: true)
    }

    private func notify(force: Bool = false) {
        let now = Date()
        guard force || now.timeIntervalSince(lastNotify) > 0.25 else { return }
        lastNotify = now
        onChange?(record)
    }
}
