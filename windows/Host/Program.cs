using System.Diagnostics;
using Nexonic.Core;

// Browser helper: answers the extension over native messaging and hands downloads to the app.
var input = Console.OpenStandardInput();
var output = Console.OpenStandardOutput();
const string Version = "0.1.0";

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
                msg["userAgent"]?.GetValue<string>());
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
