namespace Nexonic.Core;

public enum DownloadStatus { Queued, Probing, Downloading, Paused, Finished, Failed }

/// <summary>One byte range. End is inclusive; -1 means "until the server stops" (size unknown).</summary>
public sealed class Segment
{
    public int Id { get; set; }
    public long Start { get; set; }
    public long End { get; set; }
    public long Written { get; set; }

    public long? Length => End < 0 ? null : End - Start + 1;
    public long Remaining => End < 0 ? long.MaxValue : Math.Max(0, End - Start + 1 - Written);
    public bool IsComplete => End >= 0 && Written >= End - Start + 1;
}

public sealed class DownloadRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Url { get; set; } = "";
    public string? Referrer { get; set; }
    public string? Cookies { get; set; }
    public string? UserAgent { get; set; }
    public string FileName { get; set; } = "";
    public string Folder { get; set; } = "";
    public long? Size { get; set; }
    public bool AcceptsRanges { get; set; }
    public DownloadStatus Status { get; set; } = DownloadStatus.Queued;
    public List<Segment> Segments { get; set; } = new();
    public DateTime Added { get; set; } = DateTime.Now;
    public DateTime? Finished { get; set; }
    public string? Error { get; set; }
    public string? SavedPath { get; set; }

    public long Received { get { lock (Segments) return Segments.Sum(s => s.Written); } }
    public double Progress => Size is > 0 ? Math.Min(1, (double)Received / Size.Value) : 0;
    public FileCategory Category => FileCategories.Of(FileName);
    public bool IsActive => Status is DownloadStatus.Probing or DownloadStatus.Downloading;
}

public sealed class AppSettings
{
    public int Connections { get; set; } = 8;
    public int MaxActive { get; set; } = 3;
    public int SpeedLimitKBps { get; set; }
    public string Folder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
    public bool SortIntoCategoryFolders { get; set; }
}

public static class AppPaths
{
    public static string Support => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexonic Download Manager");
    public static string Inbox => Path.Combine(Support, "Inbox");
    public static string Parts => Path.Combine(Support, "Parts");
    public static string Downloads => Path.Combine(Support, "downloads.json");
    public static string Settings => Path.Combine(Support, "settings.json");
}
