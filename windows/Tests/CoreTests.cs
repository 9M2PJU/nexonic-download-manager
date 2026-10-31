using Nexonic.Core;
using Xunit;

public class CoreTests
{
    [Fact]
    public void PlanCoversWholeFile()
    {
        var segs = SegmentPlanner.Plan(10_000_000, 8);
        Assert.Equal(8, segs.Count);
        Assert.Equal(0, segs[0].Start);
        Assert.Equal(9_999_999, segs[^1].End);
        for (var i = 1; i < segs.Count; i++) Assert.Equal(segs[i - 1].End + 1, segs[i].Start);
        Assert.Single(SegmentPlanner.Plan(600_000, 8));
        Assert.Equal(-1, SegmentPlanner.Plan(null, 8)[0].End);
    }

    [Fact]
    public void SplitHalvesLargestRemaining()
    {
        var segs = SegmentPlanner.Plan(8_000_000, 2);
        segs[0].Written = segs[0].Length!.Value;
        segs[1].Written = 1_000_000;
        var sp = SegmentPlanner.SplitLargest(segs, 2)!.Value;
        Assert.Equal(1, sp.Index);
        Assert.Equal(7_999_999, sp.Added.End);
        Assert.Equal(sp.NewEnd + 1, sp.Added.Start);
        segs[1].Written = segs[1].Length!.Value - 100;
        Assert.Null(SegmentPlanner.SplitLargest(segs, 2));
    }

    [Fact]
    public void FileNamesAreSafe()
    {
        Assert.Equal("report.pdf", FileNames.FromContentDisposition("attachment; filename=\"report.pdf\""));
        Assert.Equal("résumé.pdf", FileNames.FromContentDisposition("attachment; filename=a.txt; filename*=UTF-8''r%C3%A9sum%C3%A9.pdf"));
        Assert.Equal("My File.zip", FileNames.FromUrl("https://example.com/files/My%20File.zip?x=1"));
        Assert.Equal("_.._etc_passwd", FileNames.Sanitize("../../etc/passwd"));
        Assert.Equal("_CON.txt", FileNames.Sanitize("CON.txt"));
        Assert.Equal("a_b_.txt", FileNames.Sanitize("a:b?.txt"));
        var dir = Directory.CreateTempSubdirectory().FullName;
        File.WriteAllText(Path.Combine(dir, "a.zip"), "");
        Assert.Equal("a (1).zip", Path.GetFileName(FileNames.Unique("a.zip", dir)));
    }

    [Fact]
    public void CategoriesAndLimiter()
    {
        Assert.Equal(FileCategory.Video, FileCategories.Of("x.MKV"));
        Assert.Equal(FileCategory.Programs, FileCategories.Of("setup.exe"));
        var l = new SpeedLimiter(1000);
        var t = DateTime.UtcNow.AddSeconds(1);
        Assert.Equal(TimeSpan.Zero, l.Consume(1000, t));
        Assert.Equal(0.5, l.Consume(500, t).TotalSeconds, 2);
    }

    [Fact]
    public void NativeMessagesAndInbox()
    {
        var ms = new MemoryStream();
        NativeMessaging.Write(ms, new { type = "ping" });
        ms.Position = 0;
        Assert.Equal("ping", NativeMessaging.Read(ms)!["type"]!.GetValue<string>());
        Assert.False(new DownloadJob("file:///c:/windows/win.ini").IsValid);
        Assert.True(new DownloadJob("https://example.com/a.zip").IsValid);
        var inbox = new Inbox(Directory.CreateTempSubdirectory().FullName);
        inbox.Drop(new DownloadJob("https://example.com/a.zip", Cookies: "s=1"));
        Assert.Equal("s=1", inbox.Collect().Single().Cookies);
        Assert.Empty(inbox.Collect());
    }
}
