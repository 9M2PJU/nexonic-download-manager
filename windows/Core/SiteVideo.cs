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

    public sealed record Progress(long Downloaded, long? Total, double? Speed);

    const string ProgressTag = "NDMP";
    const string FileTag = "NDMF";

    static double? Num(string s) => double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;

    public static Progress? ParseProgress(string line)
    {
        var p = line.Split(' ');
        if (p.Length < 5 || p[0] != ProgressTag || Num(p[1]) is not { } done) return null;
        var total = Num(p[2]) ?? Num(p[3]);
        return new Progress((long)done, total is { } t ? (long)t : null, Num(p[4]));
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

    /// <summary>The command for one video. folder gets the finished file, temp the partial one (so it can resume).</summary>
    public static List<string> Arguments(string url, string folder, string temp, string? ffmpeg, string? quickJs, bool audioOnly,
        string? referrer, string? cookies, string? userAgent, string? username, string? password, int speedLimitKBps)
    {
        var a = new List<string>
        {
            "--ignore-config", "--no-playlist", "--newline", "--no-colors", "--no-mtime", "--continue",
            "--progress-template", $"download:{ProgressTag} %(progress.downloaded_bytes)s %(progress.total_bytes)s %(progress.total_bytes_estimate)s %(progress.speed)s",
            "--print", $"after_move:{FileTag} %(filepath)s",
            "-P", $"home:{folder}", "-P", $"temp:{temp}",
            "-o", "%(title).150B [%(id)s].%(ext)s",
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
        a.Add("--");
        a.Add(url);
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
                     Record.Referrer, Record.Cookies, Record.UserAgent, Record.Username, Record.Password, speedLimitKBps))
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
        while ((line = process.StandardOutput.ReadLine()) != null)
        {
            if (SiteVideo.ParseProgress(line) is { } p)
            {
                Record.StreamBytes = p.Downloaded;
                if (p.Total is > 0) Record.Size = p.Total;
                Notify();
            }
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
        Record.Finished = DateTime.Now;
        Notify(true);
    }
}
