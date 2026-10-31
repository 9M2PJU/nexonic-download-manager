import Foundation

public enum DownloadStatus: String, Codable, Sendable {
    case queued, probing, downloading, paused, finished, failed

    public var isActive: Bool { self == .probing || self == .downloading }
}

/// One byte range of a download. `end` is inclusive; -1 means "until the server stops" (size unknown).
public struct Segment: Codable, Equatable, Sendable {
    public var id: Int
    public var start: Int64
    public var end: Int64
    public var written: Int64

    public init(id: Int, start: Int64, end: Int64, written: Int64 = 0) {
        self.id = id
        self.start = start
        self.end = end
        self.written = written
    }

    public var length: Int64? { end < 0 ? nil : end - start + 1 }
    public var remaining: Int64 { end < 0 ? Int64.max : max(0, end - start + 1 - written) }
    public var isComplete: Bool { end >= 0 && written >= end - start + 1 }
}

public struct DownloadRecord: Codable, Identifiable, Sendable {
    public var id: UUID
    public var url: URL
    public var referrer: String?
    public var cookies: String?
    public var userAgent: String?
    public var fileName: String
    public var folder: URL
    public var size: Int64?
    public var acceptsRanges: Bool
    public var status: DownloadStatus
    public var segments: [Segment]
    public var added: Date
    public var finished: Date?
    public var error: String?
    public var savedPath: String?

    public init(url: URL, folder: URL, fileName: String? = nil, referrer: String? = nil, cookies: String? = nil,
                userAgent: String? = nil) {
        id = UUID()
        self.url = url
        self.folder = folder
        self.fileName = fileName.map(FileNames.sanitize) ?? FileNames.fromURL(url)
        self.referrer = referrer
        self.cookies = cookies
        self.userAgent = userAgent
        size = nil
        acceptsRanges = false
        status = .queued
        segments = []
        added = Date()
    }

    public var received: Int64 { segments.reduce(0) { $0 + $1.written } }
    public var progress: Double {
        guard let size, size > 0 else { return 0 }
        return min(1, Double(received) / Double(size))
    }
    public var category: FileCategory { FileCategory(fileName: fileName) }
}

public struct AppSettings: Codable, Equatable, Sendable {
    public var connections: Int = 8
    public var maxActive: Int = 3
    public var speedLimitKBps: Int = 0
    public var folder: URL = FileManager.default.urls(for: .downloadsDirectory, in: .userDomainMask)[0]
    public var sortIntoCategoryFolders: Bool = false
    public var notifications: Bool = true

    public init() {}
}
