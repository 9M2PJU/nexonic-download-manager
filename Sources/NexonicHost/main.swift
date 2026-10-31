import Foundation
import NexonicCore

// Browser helper: answers the extension over native messaging and hands downloads to the app.
let input = FileHandle.standardInput
let output = FileHandle.standardOutput
let version = "0.1.0"

func reply(_ object: [String: Any]) {
    if let data = try? NativeMessaging.encode(object) { output.write(data) }
}

func openApp(_ url: String) {
    let p = Process()
    p.executableURL = URL(fileURLWithPath: "/usr/bin/open")
    p.arguments = ["-g", url]
    try? p.run()
}

while let message = try? NativeMessaging.read(from: input) {
    switch message["type"] as? String {
    case "ping":
        reply(["ok": true, "version": version])
    case "download":
        let job = DownloadJob(
            url: message["url"] as? String ?? "",
            fileName: message["fileName"] as? String,
            referrer: message["referrer"] as? String,
            cookies: message["cookies"] as? String,
            userAgent: message["userAgent"] as? String
        )
        guard job.validURL != nil else {
            reply(["ok": false, "error": "Only http and https links can be downloaded"])
            continue
        }
        do {
            try Inbox().drop(job)
            openApp("nexonicdm://inbox")
            reply(["ok": true])
        } catch {
            reply(["ok": false, "error": error.localizedDescription])
        }
    case "open":
        openApp("nexonicdm://show")
        reply(["ok": true])
    default:
        reply(["ok": false, "error": "Unknown message"])
    }
}
