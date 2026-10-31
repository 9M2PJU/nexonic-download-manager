namespace Nexonic.Core;

public static class SegmentPlanner
{
    public const long MinSegment = 512 * 1024;
    public const long MinSplit = 1 << 20;

    /// <summary>Equal segments of at least 512 KB, between 1 and 32 of them.</summary>
    public static List<Segment> Plan(long? size, int connections)
    {
        if (size is not > 0) return new() { new Segment { Id = 0, Start = 0, End = -1 } };
        var n = (int)Math.Max(1, Math.Min(Math.Min(connections, 32), Math.Max(1, size.Value / MinSegment)));
        var basis = size.Value / n;
        var list = new List<Segment>();
        long start = 0;
        for (var i = 0; i < n; i++)
        {
            var end = i == n - 1 ? size.Value - 1 : start + basis - 1;
            list.Add(new Segment { Id = i, Start = start, End = end });
            start = end + 1;
        }
        return list;
    }

    /// <summary>Halves the unfinished segment with the most bytes left (at least 1 MB).</summary>
    public static (int Index, long NewEnd, Segment Added)? SplitLargest(IReadOnlyList<Segment> segments, int nextId)
    {
        int best = -1;
        long bestLeft = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            var s = segments[i];
            if (s.End >= 0 && !s.IsComplete && s.Remaining > bestLeft) { best = i; bestLeft = s.Remaining; }
        }
        if (best < 0 || bestLeft < MinSplit) return null;
        var seg = segments[best];
        var mid = seg.Start + seg.Written + bestLeft / 2;
        return (best, mid - 1, new Segment { Id = nextId, Start = mid, End = seg.End });
    }
}
