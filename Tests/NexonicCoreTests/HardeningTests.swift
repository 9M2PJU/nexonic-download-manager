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
