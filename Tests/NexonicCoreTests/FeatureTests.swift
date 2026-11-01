import XCTest
@testable import NexonicCore

final class FeatureTests: XCTestCase {
    func testBatchNumbersKeepTheirWidth() throws {
        let urls = try BatchPattern.expand("https://e.com/file[001-100].jpg")
        XCTAssertEqual(urls.count, 100)
        XCTAssertEqual(urls.first, "https://e.com/file001.jpg")
        XCTAssertEqual(urls.last, "https://e.com/file100.jpg")
        XCTAssertEqual(try BatchPattern.expand("a[1-3]"), ["a1", "a2", "a3"])
        XCTAssertEqual(try BatchPattern.expand("p[0-20:10]"), ["p0", "p10", "p20"])
    }

    func testBatchLettersAndSeveralRanges() throws {
        XCTAssertEqual(try BatchPattern.expand("[a-c].png"), ["a.png", "b.png", "c.png"])
        XCTAssertEqual(try BatchPattern.expand("s[1-2]e[1-2]"), ["s1e1", "s1e2", "s2e1", "s2e2"])
        XCTAssertEqual(try BatchPattern.expand("http://[::1]/x"), ["http://[::1]/x"])
        XCTAssertThrowsError(try BatchPattern.expand("[1-200][1-200]"))
    }

    func testChecksums() throws {
        let abc = Data("abc".utf8)
        XCTAssertEqual(Checksum.compute(.md5, data: abc), "900150983cd24fb0d6963f7d28e17f72")
        XCTAssertEqual(Checksum.compute(.sha1, data: abc), "a9993e364706816aba3e25717850c26c9cd0d89d")
        XCTAssertEqual(Checksum.compute(.sha256, data: abc), "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad")
        XCTAssertEqual(Checksum.parse("SHA-256: BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")?.0, .sha256)
        XCTAssertNil(Checksum.parse("hello"))
        let file = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try abc.write(to: file)
        defer { try? FileManager.default.removeItem(at: file) }
        XCTAssertEqual(try Checksum.compute(.md5, file: file), "900150983cd24fb0d6963f7d28e17f72")
    }

    func testLinksAndScope() {
        let base = URL(string: "https://e.com/files/index.html")!
        let html = ##"<a href="a.zip">a</a> <a href='/files/sub/'>s</a> <img src=pic.jpg> <a href="#top">t</a> <a href="mailto:x@e.com">m</a> <a href="a.zip#x">dup</a>"##
        let links = LinkExtractor.links(in: html, base: base).map(\.absoluteString)
        XCTAssertEqual(links, ["https://e.com/files/a.zip", "https://e.com/files/sub/", "https://e.com/files/pic.jpg"])
        let o = GrabOptions(start: base, depth: 2, types: ["zip"])
        XCTAssertTrue(SiteGrabber.inScope(URL(string: "https://e.com/files/sub/x.html")!, of: o))
        XCTAssertFalse(SiteGrabber.inScope(URL(string: "https://e.com/other/x.html")!, of: o))
        XCTAssertFalse(SiteGrabber.inScope(URL(string: "https://other.com/files/x.html")!, of: o))
        XCTAssertTrue(SiteGrabber.wanted(URL(string: "https://e.com/files/a.ZIP")!, o))
        XCTAssertFalse(SiteGrabber.wanted(URL(string: "https://e.com/files/pic.jpg")!, o))
    }

    func testQueueTimetable() {
        var cal = Calendar(identifier: .gregorian)
        cal.timeZone = TimeZone(identifier: "UTC")!
        let monday2am = Date(timeIntervalSince1970: 1_793_584_800)  // 2026-11-02 02:00 UTC, a Monday
        XCTAssertEqual(QueueClock.weekday(monday2am, calendar: cal), 1)
        var q = DownloadQueue(name: "Night")
        XCTAssertFalse(QueueClock.shouldStart(q, at: monday2am, calendar: cal))
        q.startEnabled = true
        XCTAssertTrue(QueueClock.shouldStart(q, at: monday2am, calendar: cal))
        q.days = [6, 7]
        XCTAssertFalse(QueueClock.shouldStart(q, at: monday2am, calendar: cal))
        q.days = [1]
        q.lastStartDay = QueueClock.dayKey(monday2am, calendar: cal)
        XCTAssertFalse(QueueClock.shouldStart(q, at: monday2am, calendar: cal))
        q.running = true
        q.stopEnabled = true
        q.stopTime = "02:00"
        XCTAssertTrue(QueueClock.shouldStop(q, at: monday2am, calendar: cal))
    }

    func testClipboardLinks() {
        let types = FileTypes.parse(".zip, mp4 PDF")
        XCTAssertEqual(FileTypes.downloadLink(in: " https://e.com/a.zip \n", types: types)?.absoluteString, "https://e.com/a.zip")
        XCTAssertNil(FileTypes.downloadLink(in: "https://e.com/page.html", types: types))
        XCTAssertNil(FileTypes.downloadLink(in: "see https://e.com/a.zip", types: types))
        XCTAssertNotNil(FileTypes.downloadLink(in: "https://e.com/doc.PDF?x=1", types: types))
    }

    func testOldSettingsStillLoad() throws {
        let old = #"{"connections":4,"maxActive":2,"speedLimitKBps":0,"folder":"file:///tmp/","sortIntoCategoryFolders":true,"notifications":false}"#
        let s = try JSONDecoder().decode(AppSettings.self, from: Data(old.utf8))
        XCTAssertEqual(s.connections, 4)
        XCTAssertTrue(s.sortIntoCategoryFolders)
        XCTAssertTrue(s.monitorClipboard)
        XCTAssertEqual(s.proxy.mode, .system)
    }
}

final class CommandLineTests: XCTestCase {
    func testLinksAndOptions() {
        guard case .jobs(let jobs) = CommandLineTool.parse(["https://e.com/a.zip", "--queue", "Night", "--later", "--limit", "200"]) else {
            return XCTFail("expected jobs")
        }
        XCTAssertEqual(jobs.count, 1)
        XCTAssertEqual(jobs[0].queue, "Night")
        XCTAssertEqual(jobs[0].start, false)
        XCTAssertEqual(jobs[0].speedLimitKBps, 200)
    }

    func testBatchAndErrors() {
        guard case .jobs(let jobs) = CommandLineTool.parse(["batch", "https://e.com/f[1-5].jpg"]) else { return XCTFail("expected jobs") }
        XCTAssertEqual(jobs.count, 5)
        XCTAssertEqual(CommandLineTool.parse(["help"]), .help)
        if case .failure = CommandLineTool.parse(["https://e.com/a", "https://e.com/b", "--name", "x"]) {} else { XCTFail() }
        if case .failure = CommandLineTool.parse(["https://e.com/a", "--checksum", "nope"]) {} else { XCTFail() }
        XCTAssertTrue(CommandLineTool.isCommand(["https://e.com/a"]))
        XCTAssertFalse(CommandLineTool.isCommand(["chrome-extension://abc/"]))
        XCTAssertFalse(CommandLineTool.isCommand([]))
    }

    func testWords() {
        XCTAssertEqual(CommandLineTool.words(#""/opt/scan tool/scan" --quiet  -x"#), ["/opt/scan tool/scan", "--quiet", "-x"])
        XCTAssertEqual(CommandLineTool.words(""), [])
    }
}

final class HLSTests: XCTestCase {
    func testMasterPicksBestQualityAndSound() throws {
        let text = """
        #EXTM3U
        #EXT-X-MEDIA:TYPE=AUDIO,GROUP-ID="aud",NAME="English",DEFAULT=YES,URI="audio/en.m3u8"
        #EXT-X-STREAM-INF:BANDWIDTH=800000,RESOLUTION=640x360,CODECS="avc1.4d401e,mp4a.40.2",AUDIO="aud"
        low/index.m3u8
        #EXT-X-STREAM-INF:BANDWIDTH=5000000,RESOLUTION=1920x1080,AUDIO="aud"
        hd/index.m3u8
        #EXT-X-STREAM-INF:BANDWIDTH=2500000,RESOLUTION=1280x720,AUDIO="aud"
        mid/index.m3u8
        """
        guard case .master(let variants, let audio) = try HLS.parse(text, base: URL(string: "https://e.com/v/master.m3u8")!) else {
            return XCTFail("expected a master playlist")
        }
        XCTAssertEqual(variants.count, 3)
        XCTAssertEqual(variants[0].audioGroup, "aud")
        XCTAssertEqual(HLS.best(variants)?.url.absoluteString, "https://e.com/v/hd/index.m3u8")
        XCTAssertEqual(audio["aud"]?.absoluteString, "https://e.com/v/audio/en.m3u8")
    }

    func testMediaPlaylist() throws {
        let text = """
        #EXTM3U
        #EXT-X-TARGETDURATION:6
        #EXT-X-MAP:URI="init.mp4"
        #EXTINF:6.0,
        seg1.m4s
        #EXTINF:6.0,
        https://cdn.e.com/seg2.m4s
        #EXT-X-ENDLIST
        """
        guard case .media(let m) = try HLS.parse(text, base: URL(string: "https://e.com/v/hd/index.m3u8")!) else {
            return XCTFail("expected a media playlist")
        }
        XCTAssertEqual(m.segments.map(\.absoluteString), ["https://e.com/v/hd/seg1.m4s", "https://cdn.e.com/seg2.m4s"])
        XCTAssertEqual(m.initSegment?.absoluteString, "https://e.com/v/hd/init.mp4")
        XCTAssertTrue(m.isFragmentedMP4)
        XCTAssertEqual(m.encryption, "NONE")
    }

    func testProtectedAndInvalid() throws {
        let text = "#EXTM3U\n#EXT-X-KEY:METHOD=SAMPLE-AES,URI=\"skd://x\"\n#EXTINF:6,\na.ts\n"
        guard case .media(let m) = try HLS.parse(text, base: URL(string: "https://e.com/i.m3u8")!) else { return XCTFail() }
        XCTAssertEqual(m.encryption, "SAMPLE-AES")
        XCTAssertThrowsError(try HLS.parse("<html>", base: URL(string: "https://e.com/")!))
        XCTAssertTrue(HLS.isPlaylistURL(URL(string: "https://e.com/live/index.M3U8?token=1")!))
    }
}
