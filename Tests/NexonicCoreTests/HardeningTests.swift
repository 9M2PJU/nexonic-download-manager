import XCTest
@testable import NexonicCore

/// Security and integrity rules: private headers stay on the download's own server, resumes never mix two files.
final class HardeningTests: XCTestCase {
    func testPrivateHeadersOnlyForTheSameServer() {
        let origin = URL(string: "https://files.example.com/a")!
        XCTAssertTrue(Network.sameSite(URL(string: "https://FILES.example.com/b")!, origin))
        XCTAssertTrue(Network.sameSite(URL(string: "https://files.example.com:443/b")!, origin))
        XCTAssertFalse(Network.sameSite(URL(string: "http://files.example.com/b")!, origin))  // no step down to http
        XCTAssertFalse(Network.sameSite(URL(string: "https://cdn.example.com/b")!, origin))
        XCTAssertFalse(Network.sameSite(URL(string: "https://files.example.com:8443/b")!, origin))
    }

    func testResumeValidators() {
        func response(_ headers: [String: String]) -> HTTPURLResponse {
            HTTPURLResponse(url: URL(string: "https://e.com/f")!, statusCode: 206, httpVersion: nil, headerFields: headers)!
        }
        XCTAssertEqual(Network.validator(response(["ETag": "\"v1\"", "Last-Modified": "Mon, 02 Nov 2026 10:00:00 GMT"])), "\"v1\"")
        XCTAssertEqual(Network.validator(response(["ETag": "W/\"weak\"", "Last-Modified": "Mon, 02 Nov 2026 10:00:00 GMT"])),
                       "Mon, 02 Nov 2026 10:00:00 GMT")
        XCTAssertNil(Network.validator(response([:])))
        XCTAssertTrue(Network.sameVersion(nil, "\"v2\""))  // lists saved before 0.6.2
        XCTAssertFalse(Network.sameVersion("\"v1\"", "\"v2\""))
    }

    func testContentRangeMustMatchTheRequest() {
        XCTAssertTrue(Network.rangeMatches("bytes 5-9/10", from: 5, size: 10))
        XCTAssertTrue(Network.rangeMatches("bytes 5-9/*", from: 5, size: 10))
        XCTAssertFalse(Network.rangeMatches("bytes 0-4/10", from: 5, size: 10))  // wrong part
        XCTAssertFalse(Network.rangeMatches("bytes 5-9/99", from: 5, size: 10))  // another file
        XCTAssertFalse(Network.rangeMatches(nil, from: 5, size: 10))
    }

    func testOlderListsStillLoad() throws {
        let json = #"[{"id":"6F9619FF-8B86-D011-B42D-00C04FC964FF","url":"https://e.com/a.zip","fileName":"a.zip","folder":"file:///tmp/","acceptsRanges":true,"status":"paused","segments":[],"added":0}]"#
        let list = try JSONDecoder().decode([DownloadRecord].self, from: Data(json.utf8))
        XCTAssertNil(list[0].validator)
    }
}

/// Cookies and passwords leave the JSON files for the Keychain; damaged lists are kept.
final class StorageTests: XCTestCase {
    func testSecretsMoveOutOfTheFiles() throws {
        var a = DownloadRecord(url: URL(string: "https://e.com/a")!, folder: URL(fileURLWithPath: "/tmp"), cookies: "sid=1")
        a.username = "me"
        a.password = "pw"
        var settings = AppSettings()
        settings.proxy.password = "proxy-pw"
        let secrets = Secrets.collect([a], settings)
        XCTAssertEqual(secrets.count, 3)
        let fileList = Secrets.stripped([a])
        let fileJSON = String(decoding: try JSONEncoder().encode(fileList), as: UTF8.self)
        XCTAssertFalse(fileJSON.contains("sid=1") || fileJSON.contains("\"pw\""))
        XCTAssertTrue(fileJSON.contains("\"me\""))  // user names are not secret
        XCTAssertEqual(Secrets.stripped(settings).proxy.password, "")

        var back = fileList
        var backSettings = Secrets.stripped(settings)
        Secrets.fill(&back, &backSettings, from: secrets)
        XCTAssertEqual(back[0].cookies, "sid=1")
        XCTAssertEqual(back[0].password, "pw")
        XCTAssertEqual(backSettings.proxy.password, "proxy-pw")

        // a value still in the file (saved by 0.6.1) is kept
        var old = [a]
        old[0].cookies = "newer=2"
        Secrets.fill(&old, &backSettings, from: secrets)
        XCTAssertEqual(old[0].cookies, "newer=2")

        let store = MemoryStore(nil)  // Keychain denied
        XCTAssertNil(store.read())
    }

    func testDamagedListIsKept() throws {
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent(UUID().uuidString)
        try FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        let file = dir.appendingPathComponent("downloads.json")
        try Data("[{\"url\": ".utf8).write(to: file)
        XCTAssertNil(SavedFile.load([DownloadRecord].self, from: file))
        let kept = try FileManager.default.contentsOfDirectory(atPath: dir.path).filter { $0.hasPrefix("downloads.unreadable-") }
        XCTAssertEqual(kept.count, 1)
    }
}
