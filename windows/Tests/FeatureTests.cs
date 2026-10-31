using Nexonic.Core;
using Xunit;

public class FeatureTests
{
    [Fact]
    public void BatchNumbersKeepTheirWidth()
    {
        var urls = BatchPattern.Expand("https://e.com/file[001-100].jpg");
        Assert.Equal(100, urls.Count);
        Assert.Equal("https://e.com/file001.jpg", urls[0]);
        Assert.Equal("https://e.com/file100.jpg", urls[^1]);
        Assert.Equal(new[] { "a1", "a2", "a3" }, BatchPattern.Expand("a[1-3]"));
        Assert.Equal(new[] { "p0", "p10", "p20" }, BatchPattern.Expand("p[0-20:10]"));
    }

    [Fact]
    public void BatchLettersAndSeveralRanges()
    {
        Assert.Equal(new[] { "a.png", "b.png", "c.png" }, BatchPattern.Expand("[a-c].png"));
        Assert.Equal(new[] { "s1e1", "s1e2", "s2e1", "s2e2" }, BatchPattern.Expand("s[1-2]e[1-2]"));
        Assert.Equal(new[] { "http://[::1]/x" }, BatchPattern.Expand("http://[::1]/x"));
        Assert.Throws<ArgumentException>(() => BatchPattern.Expand("[1-200][1-200]"));
    }

    [Fact]
    public void Checksums()
    {
        var abc = "abc"u8.ToArray();
        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", Checksum.Compute(ChecksumAlgorithm.Md5, abc));
        Assert.Equal("a9993e364706816aba3e25717850c26c9cd0d89d", Checksum.Compute(ChecksumAlgorithm.Sha1, abc));
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", Checksum.Compute(ChecksumAlgorithm.Sha256, abc));
        Assert.Equal(ChecksumAlgorithm.Sha256, Checksum.Parse("SHA-256: BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD")?.Algorithm);
        Assert.Null(Checksum.Parse("hello"));
        var file = Path.GetTempFileName();
        File.WriteAllBytes(file, abc);
        Assert.Equal("MD5 matches", Checksum.Verify("900150983cd24fb0d6963f7d28e17f72", file));
        File.Delete(file);
    }

    [Fact]
    public void LinksAndScope()
    {
        var b = new Uri("https://e.com/files/index.html");
        var html = "<a href=\"a.zip\">a</a> <a href='/files/sub/'>s</a> <img src=pic.jpg> <a href=\"#top\">t</a> <a href=\"mailto:x@e.com\">m</a> <a href=\"a.zip#x\">dup</a>";
        Assert.Equal(new[] { "https://e.com/files/a.zip", "https://e.com/files/sub/", "https://e.com/files/pic.jpg" },
            LinkExtractor.Links(html, b).Select(u => u.AbsoluteUri));
        var o = new GrabOptions { Start = b, Depth = 2, Types = new() { "zip" } };
        Assert.True(SiteGrabber.InScope(new Uri("https://e.com/files/sub/x.html"), o));
        Assert.False(SiteGrabber.InScope(new Uri("https://e.com/other/x.html"), o));
        Assert.False(SiteGrabber.InScope(new Uri("https://other.com/files/x.html"), o));
        Assert.True(SiteGrabber.Wanted(new Uri("https://e.com/files/a.ZIP"), o));
        Assert.False(SiteGrabber.Wanted(new Uri("https://e.com/files/pic.jpg"), o));
    }

    [Fact]
    public void QueueTimetable()
    {
        var monday2am = new DateTime(2026, 11, 2, 2, 0, 0);
        Assert.Equal(1, QueueClock.Weekday(monday2am));
        var q = new DownloadQueue { Name = "Night" };
        Assert.False(QueueClock.ShouldStart(q, monday2am));
        q.StartEnabled = true;
        Assert.True(QueueClock.ShouldStart(q, monday2am));
        q.Days = new() { 6, 7 };
        Assert.False(QueueClock.ShouldStart(q, monday2am));
        q.Days = new() { 1 };
        q.LastStartDay = QueueClock.DayKey(monday2am);
        Assert.False(QueueClock.ShouldStart(q, monday2am));
        q.Running = true;
        q.StopEnabled = true;
        q.StopTime = "02:00";
        Assert.True(QueueClock.ShouldStop(q, monday2am));
    }

    [Fact]
    public void ClipboardLinks()
    {
        var types = FileTypes.Parse(".zip, mp4 PDF");
        Assert.Equal("https://e.com/a.zip", FileTypes.DownloadLink(" https://e.com/a.zip \n", types)?.AbsoluteUri);
        Assert.Null(FileTypes.DownloadLink("https://e.com/page.html", types));
        Assert.Null(FileTypes.DownloadLink("see https://e.com/a.zip", types));
        Assert.NotNull(FileTypes.DownloadLink("https://e.com/doc.PDF?x=1", types));
    }

    [Fact]
    public void CommandLine()
    {
        var r = CommandLineTool.Parse(new[] { "https://e.com/a.zip", "--queue", "Night", "--later", "--limit", "200" });
        Assert.Null(r.Error);
        Assert.Single(r.Jobs);
        Assert.Equal("Night", r.Jobs[0].Queue);
        Assert.False(r.Jobs[0].Start);
        Assert.Equal(200, r.Jobs[0].SpeedLimitKBps);
        Assert.Equal(5, CommandLineTool.Parse(new[] { "batch", "https://e.com/f[1-5].jpg" }).Jobs.Count);
        Assert.True(CommandLineTool.Parse(new[] { "help" }).Help);
        Assert.NotNull(CommandLineTool.Parse(new[] { "https://e.com/a", "https://e.com/b", "--name", "x" }).Error);
        Assert.NotNull(CommandLineTool.Parse(new[] { "https://e.com/a", "--checksum", "nope" }).Error);
        Assert.True(CommandLineTool.IsCommand(new[] { "https://e.com/a" }));
        Assert.False(CommandLineTool.IsCommand(new[] { "chrome-extension://abc/" }));
        Assert.False(CommandLineTool.IsCommand(Array.Empty<string>()));
        Assert.Equal(new[] { @"C:\Scan Tool\scan.exe", "/quiet", "-x" }, CommandLineTool.Words("\"C:\\Scan Tool\\scan.exe\" /quiet  -x"));
    }

    [Fact]
    public void OldSettingsAndJobsStillLoad()
    {
        var s = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"Connections\":4,\"SortIntoCategoryFolders\":true}")!;
        Assert.Equal(4, s.Connections);
        Assert.True(s.MonitorClipboard);
        Assert.Equal(ProxyMode.System, s.Proxy.Mode);
        var j = System.Text.Json.JsonSerializer.Deserialize<DownloadJob>("{\"Url\":\"https://e.com/a\",\"Queue\":\"Night\",\"Start\":false}")!;
        Assert.Equal("Night", j.Queue);
        Assert.False(j.Start);
    }
}
