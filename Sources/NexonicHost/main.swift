import Foundation
import NexonicCore

// Browser helper (native messaging) and the `ndm` command line: both hand downloads to the app through the inbox.
let version = "0.4.0"

func openApp(_ url: String) {
    let p = Process()
    p.executableURL = URL(fileURLWithPath: "/usr/bin/open")
    p.arguments = ["-g", url]
    try? p.run()
    p.waitUntilExit()
}

let arguments = Array(CommandLine.arguments.dropFirst())
if CommandLineTool.isCommand(arguments) {
    switch CommandLineTool.parse(arguments) {
    case .help:
        print(CommandLineTool.usage)
    case .failure(let message):
        FileHandle.standardError.write(Data("ndm: \(message)\n".utf8))
        exit(2)
    case .jobs(let jobs):
        do {
            for job in jobs { try Inbox().drop(job) }
            openApp("nexonicdm://inbox")
            print("Added \(jobs.count) download\(jobs.count == 1 ? "" : "s")")
        } catch {
            FileHandle.standardError.write(Data("ndm: \(error.localizedDescription)\n".utf8))
            exit(1)
        }
    }
    exit(0)
}

let input = FileHandle.standardInput
let output = FileHandle.standardOutput

func reply(_ object: [String: Any]) {
    if let data = try? NativeMessaging.encode(object) { output.write(data) }
}

while let message = try? NativeMessaging.read(from: input) {
    switch message["type"] as? String {
    case "ping":
        reply(["ok": true, "version": version])
    case "download":
        var job = DownloadJob(
            url: message["url"] as? String ?? "",
            fileName: message["fileName"] as? String,
            referrer: message["referrer"] as? String,
            cookies: message["cookies"] as? String,
            userAgent: message["userAgent"] as? String
        )
        if let later = message["later"] as? Bool, later { job.start = false }
        if let stream = message["stream"] as? Bool, stream { job.stream = true }
        if let site = message["site"] as? Bool, site { job.site = true }
        if let audio = message["audioOnly"] as? Bool, audio { job.audioOnly = true }
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
