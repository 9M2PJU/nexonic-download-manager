using System.Diagnostics;
using Nexonic.Core;

// Browser helper (native messaging) and the ndm command line: both hand downloads to the app through the inbox.
const string Version = "0.5.6";

if (CommandLineTool.IsCommand(args))
{
    var r = CommandLineTool.Parse(args);
    if (r.Help) { Console.WriteLine(CommandLineTool.Usage); return 0; }
    if (r.Error != null) { Console.Error.WriteLine($"ndm: {r.Error}"); return 2; }
    try
    {
        foreach (var j in r.Jobs) new Inbox().Drop(j);
        StartApp();
        Console.WriteLine($"Added {r.Jobs.Count} download{(r.Jobs.Count == 1 ? "" : "s")}");
        return 0;
    }
    catch (Exception e) { Console.Error.WriteLine($"ndm: {e.Message}"); return 1; }
}

var input = Console.OpenStandardInput();
var output = Console.OpenStandardOutput();

void StartApp()
{
    var app = Path.Combine(AppContext.BaseDirectory, "NexonicDownloadManager.exe");
    if (!File.Exists(app)) return;
    // The app is single-instance: a second start only wakes the running one, which then reads the inbox.
    try { Process.Start(new ProcessStartInfo(app, "--inbox") { UseShellExecute = false }); } catch { }
}

while (NativeMessaging.Read(input) is { } msg)
{
    var type = msg["type"]?.GetValue<string>();
    switch (type)
    {
        case "ping":
            NativeMessaging.Write(output, new { ok = true, version = Version });
            break;
        case "download":
            var job = new DownloadJob(
                msg["url"]?.GetValue<string>() ?? "",
                msg["fileName"]?.GetValue<string>(),
                msg["referrer"]?.GetValue<string>(),
                msg["cookies"]?.GetValue<string>(),
                msg["userAgent"]?.GetValue<string>()) { Start = msg["later"]?.GetValue<bool>() == true ? false : null, Stream = msg["stream"]?.GetValue<bool>() == true ? true : null,
                  Site = msg["site"]?.GetValue<bool>() == true ? true : null, AudioOnly = msg["audioOnly"]?.GetValue<bool>() == true ? true : null,
                  Size = msg["size"] is System.Text.Json.Nodes.JsonValue sv && sv.TryGetValue<double>(out var sz) && sz > 0 ? (long)sz : null,
                  FromBrowser = msg["browser"]?.GetValue<bool>() == true ? true : null };
            if (!job.IsValid)
            {
                NativeMessaging.Write(output, new { ok = false, error = "Only http and https links can be downloaded" });
                break;
            }
            try
            {
                new Inbox().Drop(job);
                StartApp();
                NativeMessaging.Write(output, new { ok = true });
            }
            catch (Exception e)
            {
                NativeMessaging.Write(output, new { ok = false, error = e.Message });
            }
            break;
        case "open":
            StartApp();
            NativeMessaging.Write(output, new { ok = true });
            break;
        default:
            NativeMessaging.Write(output, new { ok = false, error = "Unknown message" });
            break;
    }
}
return 0;
