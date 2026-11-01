using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Nexonic.Core;

/// <summary>
/// Videos from YouTube, Instagram, Facebook, X, TikTok and 1,000+ other sites, saved with the bundled yt-dlp
/// in the best quality (picture and sound joined by the bundled ffmpeg).
/// </summary>
public static class SiteVideo
{
    public static readonly string[] Hosts =
    {
        "youtube.com", "youtu.be", "youtube-nocookie.com", "instagram.com", "facebook.com", "fb.watch", "x.com", "twitter.com",
        "tiktok.com", "vimeo.com", "dailymotion.com", "reddit.com", "redd.it", "twitch.tv", "soundcloud.com", "linkedin.com",
        "pinterest.com", "threads.net", "bilibili.com", "rumble.com", "streamable.com", "bandcamp.com", "mixcloud.com",
    };

    public static bool IsSupported(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u)) return false;
        var host = u.Host.ToLowerInvariant();
        return Hosts.Any(h => host == h || host.EndsWith("." + h));
    }

    public sealed record Progress(long Downloaded, long? Total, double? Speed, string? Part = null, string? Label = null);

    /// <summary>
    /// Adds up the progress of the pieces of one video (picture and sound are downloaded one after the other).
    /// Each piece only ever grows, so the jumps of fragmented downloads can't inflate the total.
    /// </summary>
    public sealed class PartProgress
    {
        readonly Dictionary<string, long> done = new();
        readonly Dictionary<string, long> totals = new();
        readonly Dictionary<string, string> labels = new();
        readonly List<string> order = new();

        /// <summary>Each piece in the order it started, for the progress window.</summary>
        public List<VideoPart> Pieces => order.Select((k, i) => new VideoPart
        {
            Name = labels.GetValueOrDefault(k) ?? (order.Count > 1 ? $"Part {i + 1}" : "Video"),
            Done = done.GetValueOrDefault(k),
            Total = totals.TryGetValue(k, out var t) ? t : null,
        }).ToList();

        public (long Done, long? Total) Add(Progress p)
        {
            var key = p.Part ?? "";
            if (!done.ContainsKey(key)) order.Add(key);
            if (p.Label != null) labels[key] = p.Label;
            done[key] = Math.Max(done.GetValueOrDefault(key), p.Downloaded);
            if (p.Total is > 0) totals[key] = p.Total.Value;
            var sum = done.Values.Sum();
            var known = done.Keys.Sum(k => Math.Max(totals.GetValueOrDefault(k), done[k]));
            return (sum, totals.Count == 0 ? null : Math.Max(known, sum));
        }
    }

    const string ProgressTag = "NDMP";
    const string FileTag = "NDMF";

    static double? Num(string s) => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

    public static Progress? ParseProgress(string line)
    {
        var p = line.Split(' ');
        if (p.Length < 5 || p[0] != ProgressTag || Num(p[1]) is not { } done) return null;
        var total = Num(p[2]) ?? Num(p[3]);
        string? label = null;
        if (p.Length >= 8) label = p[7] == "none" ? "Sound" : "Picture" + (int.TryParse(p[6], out var h) ? $" {h}p" : "");
        return new Progress((long)done, total is { } t ? (long)t : null, Num(p[4]), p.Length >= 6 && p[5] != "NA" ? p[5] : null, label);
    }

    /// <summary>What yt-dlp is doing after the pieces are downloaded ("[Merger] Merging formats into …").</summary>
    public static string? Step(string line)
    {
        if (line.StartsWith("[Merger]")) return "Joining picture and sound…";
        if (line.StartsWith("[ExtractAudio]")) return "Converting the sound…";
        if (line.StartsWith("[Fixup") || line.StartsWith("[VideoConvertor]") || line.StartsWith("[VideoRemuxer]")) return "Finishing the file…";
        return null;
    }

    /// <summary>The video's real name from "[download] Destination: …\Title.f137.mp4" (the piece number is dropped).</summary>
    public static string? DestinationName(string line)
    {
        const string tag = "[download] Destination: ";
        if (!line.StartsWith(tag)) return null;
        var name = Path.GetFileName(line[tag.Length..]);
        name = Regex.Replace(name, @"\.f[0-9A-Za-z_-]+(\.[^.]+)$", "$1");
        return name.Length > 0 ? name : null;
    }

    public static string? SavedFile(string line) => line.StartsWith(FileTag + " ") ? line[(FileTag.Length + 1)..] : null;

    public static string Error(string text)
    {
        var line = text.Split('\n').LastOrDefault(l => l.Contains("ERROR:")) ?? "";
        var msg = line.Split("ERROR:").Last().Trim();
        msg = Regex.Replace(msg, @"^\[[^\]]+\] [^:]+: ", "");
        if (msg.Length == 0) return "The video could not be downloaded";
        if (msg.Contains("DRM", StringComparison.OrdinalIgnoreCase)) return "This video is copy-protected and can't be saved";
        if (msg.Contains("sign in", StringComparison.OrdinalIgnoreCase) || msg.Contains("login", StringComparison.OrdinalIgnoreCase))
            return "The site wants you to be signed in. Open the video in your browser while signed in and send it from there";
        return msg;
    }

    public sealed record Info(string Title, string Ext, long? Size);

    /// <summary>Video details older than this are looked up again (the links inside them expire).</summary>
    public static readonly TimeSpan InfoMaxAge = TimeSpan.FromHours(2);

    /// <summary>
    /// The command that looks a video up without downloading it (prints its details as JSON). The saved JSON is handed to
    /// Arguments(infoJson), so the download starts right away instead of looking the video up again.
    /// </summary>
    public static List<string> InfoArguments(string url, string? quickJs, bool audioOnly, string? referrer, string? cookies,
        string? userAgent, string? username, string? password)
    {
        var a = new List<string> { "--ignore-config", "--no-playlist", "--no-colors", "-J", "--js-runtimes", "node", "--encoding", "utf-8" };
        if (quickJs != null) a.AddRange(new[] { "--js-runtimes", $"quickjs:{quickJs}" });
        if (audioOnly) a.AddRange(new[] { "-f", "ba[ext=m4a]/ba/b" });
        else a.AddRange(new[] { "-f", "bv*+ba/b", "-S", "res,fps,hdr:12,vcodec:avc,acodec:m4a", "--merge-output-format", "mp4/mkv" });
        if (!string.IsNullOrEmpty(referrer)) a.AddRange(new[] { "--referer", referrer });
        if (!string.IsNullOrEmpty(userAgent)) a.AddRange(new[] { "--user-agent", userAgent });
        if (!string.IsNullOrEmpty(cookies)) a.AddRange(new[] { "--add-header", $"Cookie:{cookies}" });
        if (!string.IsNullOrEmpty(username)) a.AddRange(new[] { "--username", username, "--password", password ?? "" });
        a.Add("--");
        a.Add(url);
        return a;
    }

    /// <summary>Title, file type and size from the JSON printed by InfoArguments.</summary>
    public static Info? ParseInfo(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var o = doc.RootElement;
            if (!o.TryGetProperty("title", out var t) || t.ValueKind != System.Text.Json.JsonValueKind.String) return null;
            static long? Size(System.Text.Json.JsonElement e)
            {
                foreach (var k in new[] { "filesize", "filesize_approx" })
                    if (e.TryGetProperty(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number) return (long)v.GetDouble();
                return null;
            }
            var total = Size(o);
            if (total == null && o.TryGetProperty("requested_formats", out var parts) && parts.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                var sizes = parts.EnumerateArray().Select(Size).ToList();
                if (sizes.Count > 0 && sizes.All(s => s != null)) total = sizes.Sum(s => s!.Value);
            }
            var ext = o.TryGetProperty("ext", out var x) && x.ValueKind == System.Text.Json.JsonValueKind.String ? x.GetString()! : "mp4";
            return new Info(t.GetString()!, ext, total);
        }
        catch { return null; }
    }

    /// <summary>The command for one video. folder gets the finished file, temp the partial one (so it can resume).</summary>
    public static List<string> Arguments(string url, string folder, string temp, string? ffmpeg, string? quickJs, bool audioOnly,
        string? referrer, string? cookies, string? userAgent, string? username, string? password, int speedLimitKBps, string? infoJson = null)
    {
        var a = new List<string>
        {
            "--ignore-config", "--no-playlist", "--progress", "--newline", "--no-colors", "--no-mtime", "--continue",
            "--progress-template", $"download:{ProgressTag} %(progress.downloaded_bytes)s %(progress.total_bytes)s %(progress.total_bytes_estimate)s %(progress.speed)s %(info.format_id)s %(info.height)s %(info.vcodec)s",
            "--print", $"after_move:{FileTag} %(filepath)s",
            "-P", $"home:{folder}", "-P", $"temp:{temp}",
            "-o", "%(title).150B.%(ext)s",
            "--concurrent-fragments", "4",
            "--js-runtimes", "node",
            "--encoding", "utf-8",
        };
        if (quickJs != null) a.AddRange(new[] { "--js-runtimes", $"quickjs:{quickJs}" });
        if (ffmpeg != null) a.AddRange(new[] { "--ffmpeg-location", Path.GetDirectoryName(ffmpeg)! }); // folder: ffmpeg and ffprobe
        if (audioOnly) a.AddRange(new[] { "-f", "ba[ext=m4a]/ba/b" });
        else a.AddRange(new[] { "-f", "bv*+ba/b", "-S", "res,fps,hdr:12,vcodec:avc,acodec:m4a", "--merge-output-format", "mp4/mkv" });
        if (!string.IsNullOrEmpty(referrer)) a.AddRange(new[] { "--referer", referrer });
        if (!string.IsNullOrEmpty(userAgent)) a.AddRange(new[] { "--user-agent", userAgent });
        if (!string.IsNullOrEmpty(cookies)) a.AddRange(new[] { "--add-header", $"Cookie:{cookies}" });
        if (!string.IsNullOrEmpty(username)) a.AddRange(new[] { "--username", username, "--password", password ?? "" });
        if (speedLimitKBps > 0) a.AddRange(new[] { "--limit-rate", $"{speedLimitKBps}K" });
        if (infoJson != null && File.Exists(infoJson) && DateTime.UtcNow - File.GetLastWriteTimeUtc(infoJson) < InfoMaxAge)
        {
            a.Add("--load-info-json"); // already looked up while the download window was open
            a.Add(infoJson);
        }
        else
        {
            a.Add("--");
            a.Add(url);
        }
        return a;
    }
}

/// <summary>Where the video tools live: shipped next to the app, copied to a folder the app can keep up to date.</summary>
public static class SiteVideoTools
{
    static string Exe(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;
    public static string Folder => Path.Combine(AppPaths.Support, "Tools");
    public static string YtDlp => Path.Combine(Folder, Exe("yt-dlp"));
    public static string? QuickJs => File.Exists(Path.Combine(AppContext.BaseDirectory, Exe("qjs"))) ? Path.Combine(AppContext.BaseDirectory, Exe("qjs")) : null;

    /// <summary>Copies the shipped yt-dlp into the tools folder when needed and lets it update itself once a day.</summary>
    public static void Prepare()
    {
        try
        {
            var src = Path.Combine(AppContext.BaseDirectory, Exe("yt-dlp"));
            if (!File.Exists(src)) return;
            Directory.CreateDirectory(Folder);
            var stamp = Path.Combine(Folder, "bundled-date");
            var key = File.GetLastWriteTimeUtc(src).Ticks.ToString();
            if (!File.Exists(YtDlp) || (File.Exists(stamp) ? File.ReadAllText(stamp) : "") != key)
            {
                File.Copy(src, YtDlp, true);
                File.WriteAllText(stamp, key);
            }
            var checkedFile = Path.Combine(Folder, "last-update");
            if (File.Exists(checkedFile) && DateTime.UtcNow - File.GetLastWriteTimeUtc(checkedFile) < TimeSpan.FromDays(1)) return;
            File.WriteAllText(checkedFile, "");
            Process.Start(new ProcessStartInfo(YtDlp, "-U") { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true });
        }
        catch { }
    }
}

/// <summary>Runs yt-dlp for one video and reports progress; pausing stops it and resuming continues the partial file.</summary>
public sealed class SiteVideoTransfer : IDownloader
{
    public DownloadRecord Record { get; }
    public SpeedLimiter OwnLimiter { get; } = new();
    public event Action<DownloadRecord>? Changed;
    readonly bool sortIntoCategories;
    readonly int speedLimitKBps;
    readonly StringBuilder errors = new();
    Process? process;
    volatile bool stopping;
    DateTime lastNotify = DateTime.MinValue;

    string Temp => Path.Combine(AppPaths.Parts, Record.Id.ToString("N"));

    public SiteVideoTransfer(DownloadRecord record, bool sortIntoCategories = false, int speedLimitKBps = 0)
    {
        Record = record;
        this.sortIntoCategories = sortIntoCategories;
        this.speedLimitKBps = new[] { record.SpeedLimitKBps ?? 0, speedLimitKBps }.Where(x => x > 0).DefaultIfEmpty(0).Min();
    }

    void Notify(bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && (now - lastNotify).TotalMilliseconds < 250) return;
        lastNotify = now;
        Changed?.Invoke(Record);
    }

    public Task StartAsync() => Task.Run(Run);

    public void Pause()
    {
        stopping = true;
        Record.Status = DownloadStatus.Paused;
        try { process?.Kill(true); } catch { }
        Notify(true);
    }

    public void Discard()
    {
        stopping = true;
        try { process?.Kill(true); } catch { }
        try { Directory.Delete(Temp, true); } catch { }
    }

    void Fail(string message)
    {
        Record.Status = DownloadStatus.Failed;
        Record.Error = message;
        Notify(true);
    }

    void Run()
    {
        SiteVideoTools.Prepare();
        Record.Status = DownloadStatus.Probing;
        Record.Error = null;
        Record.SiteVideo = true;
        Record.VideoStep = null;
        Notify(true);
        if (!File.Exists(SiteVideoTools.YtDlp)) { Fail("The video engine is missing. Reinstall the app"); return; }
        var folder = Record.Folder;
        if (sortIntoCategories) folder = Path.Combine(folder, (Record.AudioOnly == true ? FileCategory.Music : FileCategory.Video).ToString());
        Directory.CreateDirectory(folder);
        Directory.CreateDirectory(Temp);
        var psi = new ProcessStartInfo(SiteVideoTools.YtDlp)
        {
            CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
        };
        psi.Environment["PYTHONIOENCODING"] = "utf-8";
        foreach (var arg in SiteVideo.Arguments(Record.Url, folder, Temp, FFmpeg.Path, SiteVideoTools.QuickJs, Record.AudioOnly == true,
                     Record.Referrer, Record.Cookies, Record.UserAgent, Record.Username, Record.Password, speedLimitKBps, Record.InfoJson))
            psi.ArgumentList.Add(arg);
        try
        {
            if (stopping) return;
            process = Process.Start(psi)!;
        }
        catch (Exception e) { Fail("The video engine could not start: " + e.Message); return; }
        Record.Status = DownloadStatus.Downloading;
        Notify(true);
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (errors) { errors.AppendLine(e.Data); if (errors.Length > 4000) errors.Remove(0, errors.Length - 4000); } };
        process.BeginErrorReadLine();
        string? line;
        var parts = new SiteVideo.PartProgress(); // picture and sound pieces added up
        while ((line = process.StandardOutput.ReadLine()) != null)
        {
            if (SiteVideo.ParseProgress(line) is { } p)
            {
                var sum = parts.Add(p);
                Record.StreamBytes = sum.Done;
                if (sum.Total is long t) Record.Size = t;
                Record.VideoParts = parts.Pieces;
                Notify();
            }
            else if (SiteVideo.Step(line) is { } step) { Record.VideoStep = step; Notify(true); }
            else if (SiteVideo.DestinationName(line.Trim()) is { } name) { Record.FileName = name; Notify(true); }
            else if (SiteVideo.SavedFile(line.Trim()) is { } path) Record.SavedPath = path;
        }
        process.WaitForExit();
        if (stopping) return;
        if (process.ExitCode != 0 || Record.SavedPath is not { } saved || !File.Exists(saved))
        {
            string text;
            lock (errors) text = errors.ToString();
            Fail(SiteVideo.Error(text));
            return;
        }
        try { Directory.Delete(Temp, true); } catch { }
        Record.FileName = Path.GetFileName(saved);
        Record.Size = new FileInfo(saved).Length;
        Record.StreamBytes = Record.Size;
        Record.Status = DownloadStatus.Finished;
        Record.VideoStep = null;
        Record.Finished = DateTime.Now;
        Notify(true);
    }
}
