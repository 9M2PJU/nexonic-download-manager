using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using Nexonic.Core;

namespace Nexonic.App;

/// <summary>Queue, persistence and speed for the window (UI thread only).</summary>
public sealed class DownloadManager
{
    public ObservableCollection<Row> Rows { get; } = new();
    public AppSettings Settings { get; private set; }
    public event Action<DownloadRecord>? Finished;

    readonly Dictionary<Guid, Transfer> transfers = new();
    readonly SpeedLimiter limiter = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    int ticks;

    public DownloadManager()
    {
        Settings = Load<AppSettings>(AppPaths.Settings) ?? new AppSettings();
        foreach (var r in Load<List<DownloadRecord>>(AppPaths.Downloads) ?? new())
        {
            if (r.IsActive) r.Status = DownloadStatus.Queued;
            Rows.Add(new Row(r));
        }
        limiter.SetLimit(Settings.SpeedLimitKBps * 1024);
        timer.Tick += (_, _) => Tick();
        timer.Start();
        CollectInbox();
        Schedule();
    }

    public void ApplySettings(AppSettings s)
    {
        Settings = s;
        limiter.SetLimit(s.SpeedLimitKBps * 1024);
        File.WriteAllText(AppPaths.Settings, JsonSerializer.Serialize(s));
        Schedule();
    }

    public void AddLinks(string text)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            Add(new DownloadJob(line));
    }

    public void Add(DownloadJob job)
    {
        if (!job.IsValid) return;
        var rec = new DownloadRecord
        {
            Url = job.Url, Referrer = job.Referrer, Cookies = job.Cookies, UserAgent = job.UserAgent,
            Folder = Settings.Folder,
            FileName = job.FileName is { Length: > 0 } n ? FileNames.Sanitize(n) : FileNames.FromUrl(job.Url),
        };
        Rows.Insert(0, new Row(rec));
        Schedule();
        Save();
    }

    public void CollectInbox()
    {
        foreach (var j in new Inbox().Collect()) Add(j);
    }

    public void Pause(Row row)
    {
        if (transfers.Remove(row.Record.Id, out var t)) t.Pause();
        else if (row.Record.Status == DownloadStatus.Queued) row.Record.Status = DownloadStatus.Paused;
        row.Refresh();
        Schedule();
    }

    public void Resume(Row row)
    {
        if (row.Record.Status is DownloadStatus.Paused or DownloadStatus.Failed)
        {
            row.Record.Status = DownloadStatus.Queued;
            row.Record.Error = null;
        }
        row.Refresh();
        Schedule();
    }

    public void Remove(Row row)
    {
        if (transfers.Remove(row.Record.Id, out var t)) t.Discard();
        try { Directory.Delete(Path.Combine(AppPaths.Parts, row.Record.Id.ToString("N")), true); } catch { }
        Rows.Remove(row);
        Save();
        Schedule();
    }

    public void PauseAll() { foreach (var r in Rows.ToList()) if (r.Record.IsActive || r.Record.Status == DownloadStatus.Queued) Pause(r); }
    public void ResumeAll() { foreach (var r in Rows.ToList()) if (r.Record.Status == DownloadStatus.Paused) Resume(r); }
    public void ClearFinished() { foreach (var r in Rows.Where(r => r.Record.Status == DownloadStatus.Finished).ToList()) Remove(r); }

    void Schedule()
    {
        var slots = Math.Max(1, Settings.MaxActive) - transfers.Count;
        foreach (var row in Rows.Reverse().Where(r => r.Record.Status == DownloadStatus.Queued).ToList())
        {
            if (slots-- <= 0) break;
            var t = new Transfer(row.Record, Settings.Connections, limiter, Settings.SortIntoCategoryFolders);
            transfers[row.Record.Id] = t;
            row.Record.Status = DownloadStatus.Probing;
            t.Changed += rec => System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => Changed(row, rec));
            _ = t.StartAsync();
        }
    }

    void Changed(Row row, DownloadRecord rec)
    {
        row.Refresh();
        if (rec.Status is DownloadStatus.Finished or DownloadStatus.Failed or DownloadStatus.Paused && transfers.Remove(rec.Id))
        {
            if (rec.Status == DownloadStatus.Finished) Finished?.Invoke(rec);
            Save();
            Schedule();
        }
    }

    void Tick()
    {
        foreach (var r in Rows) if (r.Record.IsActive) r.Tick();
        if (++ticks % 2 == 0) CollectInbox();
        if (ticks % 4 == 0) Save();
    }

    public void Save()
    {
        try { File.WriteAllText(AppPaths.Downloads, JsonSerializer.Serialize(Rows.Select(r => r.Record).ToList())); } catch { }
    }

    static T? Load<T>(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : default; } catch { return default; }
    }
}

/// <summary>One row in the table; raises change notifications for the bound columns.</summary>
public sealed class Row : System.ComponentModel.INotifyPropertyChanged
{
    public DownloadRecord Record { get; }
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    long lastReceived;
    double speed;

    public Row(DownloadRecord r) { Record = r; lastReceived = r.Received; }

    public string Name => Record.FileName;
    public string Size => Record.Size is long s ? Bytes(s) : "–";
    public double Percent => Record.Progress * 100;
    public string Speed => Record.IsActive && speed > 0 ? Bytes((long)speed) + "/s" : "";
    public string TimeLeft => Record.IsActive && speed > 0 && Record.Size is long s
        ? TimeSpan.FromSeconds((s - Record.Received) / speed).ToString(@"hh\:mm\:ss") : "";
    public string Status => Record.Status switch
    {
        DownloadStatus.Queued => "Queued",
        DownloadStatus.Probing => "Connecting…",
        DownloadStatus.Downloading => $"{Percent:0}%",
        DownloadStatus.Paused => "Paused",
        DownloadStatus.Finished => "Finished",
        _ => "Failed: " + Record.Error,
    };
    public string Category => Record.Category.ToString();

    public void Tick()
    {
        var r = Record.Received;
        speed = Math.Max(0, (r - lastReceived) * 2);
        lastReceived = r;
        Refresh();
    }

    public void Refresh() => PropertyChanged?.Invoke(this, new(""));

    static string Bytes(long n)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = n;
        var i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {u[i]}";
    }
}
