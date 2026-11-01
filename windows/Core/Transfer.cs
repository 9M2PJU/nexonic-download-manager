using System.Net;
using System.Net.Http.Headers;

namespace Nexonic.Core;

/// <summary>
/// One download: probes the server, splits the file into byte ranges, downloads them in parallel into part files,
/// splits the largest remaining range whenever a connection frees up, retries failed ranges and joins the parts.
/// </summary>
public sealed class Transfer : IDownloader
{
    public DownloadRecord Record { get; }
    public event Action<DownloadRecord>? Changed;

    readonly int connections;
    readonly SpeedLimiter limiter;
    /// <summary>This download's own limit, on top of the shared one (0 = none).</summary>
    public SpeedLimiter OwnLimiter { get; } = new();
    readonly bool sortIntoCategories;
    readonly HttpClient http;
    readonly Uri origin;
    CancellationTokenSource? cts;
    DateTime lastNotify = DateTime.MinValue;

    string PartsFolder => Path.Combine(AppPaths.Parts, Record.Id.ToString("N"));
    string PartPath(int id) => Path.Combine(PartsFolder, $"{id}.part");

    public Transfer(DownloadRecord record, int connections, SpeedLimiter limiter, bool sortIntoCategories = false, ProxySettings? proxy = null)
    {
        Record = record;
        this.connections = Math.Clamp(connections, 1, 32);
        this.limiter = limiter;
        this.sortIntoCategories = sortIntoCategories;
        origin = new Uri(record.Url);
        OwnLimiter.SetLimit((record.SpeedLimitKBps ?? 0) * 1024);
        var handler = Network.Handler(proxy ?? new ProxySettings());
        if (!string.IsNullOrEmpty(record.Username))
        {
            handler.Credentials = new NetworkCredential(record.Username, record.Password ?? "");
            handler.PreAuthenticate = true;
        }
        http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public Task StartAsync()
    {
        cts = new CancellationTokenSource();
        return Task.Run(() => RunAsync(cts.Token));
    }

    public void Pause()
    {
        Record.Status = DownloadStatus.Paused;
        cts?.Cancel();
        Notify(true);
    }

    public void Discard()
    {
        cts?.Cancel();
        try { Directory.Delete(PartsFolder, true); } catch { }
    }

    async Task RunAsync(CancellationToken ct)
    {
        try
        {
            Record.Status = DownloadStatus.Probing;
            Record.Error = null;
            Notify(true);
            await ProbeAsync(ct);
            Record.Status = DownloadStatus.Downloading;
            Notify(true);
            await DownloadAllAsync(ct);
            Join();
        }
        catch (Exception) when (ct.IsCancellationRequested) { /* paused or removed */ }
        catch (Exception e)
        {
            Record.Status = DownloadStatus.Failed;
            Record.Error = e.Message;
            Notify(true);
        }
    }

    async Task<HttpResponseMessage> SendAsync(string? range, CancellationToken ct)
    {
        var url = origin;
        for (var hop = 0; hop < 10; hop++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("identity"));
            if (range != null) req.Headers.TryAddWithoutValidation("Range", range);
            if (!string.IsNullOrEmpty(Record.Referrer)) req.Headers.TryAddWithoutValidation("Referer", Record.Referrer);
            if (!string.IsNullOrEmpty(Record.UserAgent)) req.Headers.TryAddWithoutValidation("User-Agent", Record.UserAgent);
            if (!string.IsNullOrEmpty(Record.Cookies) && string.Equals(url.Host, origin.Host, StringComparison.OrdinalIgnoreCase))
                req.Headers.TryAddWithoutValidation("Cookie", Record.Cookies);
            var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            var code = (int)res.StatusCode;
            if (code is >= 300 and < 400 && res.Headers.Location is { } loc)
            {
                url = loc.IsAbsoluteUri ? loc : new Uri(url, loc);
                res.Dispose();
                if (url.Scheme != "http" && url.Scheme != "https") throw new InvalidOperationException("Redirect to an unsupported address");
                continue;
            }
            return res;
        }
        throw new InvalidOperationException("Too many redirects");
    }

    async Task ProbeAsync(CancellationToken ct)
    {
        using var res = await SendAsync("bytes=0-0", ct); // headers only; the body is never read
        var code = (int)res.StatusCode;
        if (code == 401) throw new InvalidOperationException("The server asks for a user name and password (use Properties → Login)");
        if (code == 407) throw new InvalidOperationException("The proxy asks for a user name and password (Options → Connection)");
        if (code is 403 or 404 or 410 && Record.Segments.Count > 0)
            throw new InvalidOperationException($"The link has expired (HTTP {code}). Use Refresh Link to continue from where it stopped");
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"The server answered HTTP {code}");
        long? size = null;
        var ranges = false;
        if (res.StatusCode == HttpStatusCode.PartialContent && res.Content.Headers.ContentRange?.Length is long total)
        {
            size = total;
            ranges = true;
        }
        else if (res.Content.Headers.ContentLength is > 0 and var len) size = len;

        var cd = res.Content.Headers.ContentDisposition?.ToString();
        if (FileNames.FromContentDisposition(cd) is { } name && Record.FileName == FileNames.FromUrl(Record.Url)) Record.FileName = name;

        var canResume = Record.Segments.Count > 0 && ranges && Record.AcceptsRanges && size == Record.Size;
        Directory.CreateDirectory(PartsFolder);
        if (canResume)
        {
            foreach (var s in Record.Segments)
            {
                var onDisk = File.Exists(PartPath(s.Id)) ? new FileInfo(PartPath(s.Id)).Length : 0;
                s.Written = Math.Min(onDisk, s.Length ?? onDisk);
                using var f = new FileStream(PartPath(s.Id), FileMode.OpenOrCreate);
                f.SetLength(s.Written);
            }
        }
        else
        {
            try { Directory.Delete(PartsFolder, true); } catch { }
            Directory.CreateDirectory(PartsFolder);
            Record.Size = size;
            Record.AcceptsRanges = ranges;
            lock (Record.Segments)
            {
                Record.Segments = ranges
                    ? SegmentPlanner.Plan(size, connections)
                    : new() { new Segment { Id = 0, Start = 0, End = size is > 0 ? size.Value - 1 : -1 } };
            }
        }
    }

    async Task DownloadAllAsync(CancellationToken ct)
    {
        var running = new List<Task>();
        List<Segment> pending;
        lock (Record.Segments) pending = Record.Segments.Where(s => !s.IsComplete).ToList();
        foreach (var s in pending) running.Add(SegmentAsync(s, ct));
        while (running.Count > 0)
        {
            var done = await Task.WhenAny(running);
            running.Remove(done);
            await done; // rethrows a final failure
            if (!Record.AcceptsRanges || running.Count >= connections) continue;
            Segment? added = null;
            lock (Record.Segments)
            {
                var split = SegmentPlanner.SplitLargest(Record.Segments, Record.Segments.Max(s => s.Id) + 1);
                if (split is { } sp)
                {
                    Record.Segments[sp.Index].End = sp.NewEnd;
                    Record.Segments.Add(sp.Added);
                    added = sp.Added;
                }
            }
            if (added != null) running.Add(SegmentAsync(added, ct));
        }
    }

    async Task SegmentAsync(Segment seg, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (!Record.AcceptsRanges) seg.Written = 0;
                string? range = null;
                if (Record.AcceptsRanges) range = seg.End >= 0 ? $"bytes={seg.Start + seg.Written}-{seg.End}" : $"bytes={seg.Start + seg.Written}-";
                using var res = await SendAsync(range, ct);
                var ok = res.StatusCode == HttpStatusCode.PartialContent || (res.StatusCode == HttpStatusCode.OK && !Record.AcceptsRanges);
                if (!ok) throw new HttpRequestException($"HTTP {(int)res.StatusCode}");
                await using var body = await res.Content.ReadAsStreamAsync(ct);
                await using var file = new FileStream(PartPath(seg.Id), FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read, 1 << 16, true);
                file.SetLength(seg.Written);
                file.Seek(seg.Written, SeekOrigin.Begin);
                while (true)
                {
                    long left;
                    lock (Record.Segments) left = seg.Remaining;
                    if (left <= 0) return; // finished (possibly shortened by a split)
                    var n = await body.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, left)), ct);
                    if (n == 0)
                    {
                        if (seg.End < 0)
                        {
                            lock (Record.Segments) seg.End = seg.Start + seg.Written - 1;
                            Record.Size = seg.Written;
                            return;
                        }
                        throw new IOException("The connection closed early");
                    }
                    await file.WriteAsync(buffer.AsMemory(0, n), ct);
                    lock (Record.Segments) seg.Written += n;
                    Notify();
                    var a = limiter.Consume(n);
                    var b = OwnLimiter.Consume(n);
                    var wait = a > b ? a : b;
                    if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
                }
            }
            catch (Exception) when (!ct.IsCancellationRequested && attempt < 6)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, attempt - 1))), ct);
            }
        }
    }

    void Join()
    {
        var folder = Record.Folder;
        if (sortIntoCategories) folder = Path.Combine(folder, Record.Category.ToString());
        Directory.CreateDirectory(folder);
        var target = FileNames.Unique(Record.FileName, folder);
        using (var output = new FileStream(target, FileMode.CreateNew))
        {
            foreach (var s in Record.Segments.OrderBy(s => s.Start))
            {
                using var input = File.OpenRead(PartPath(s.Id));
                input.CopyTo(output);
            }
            var written = output.Length;
            if (Record.Size is long size && written != size)
            {
                output.Close();
                File.Delete(target);
                throw new IOException($"Size check failed: expected {size} bytes, got {written}");
            }
        }
        try { Directory.Delete(PartsFolder, true); } catch { }
        if (Checksum.Parse(Record.Checksum) != null) Record.ChecksumResult = Checksum.Verify(Record.Checksum!, target);
        Record.SavedPath = target;
        Record.FileName = Path.GetFileName(target);
        Record.Status = DownloadStatus.Finished;
        Record.Finished = DateTime.Now;
        Notify(true);
        http.Dispose();
    }

    void Notify(bool force = false)
    {
        var now = DateTime.UtcNow;
        if (!force && (now - lastNotify).TotalMilliseconds < 250) return;
        lastNotify = now;
        Changed?.Invoke(Record);
    }
}

public static class Network
{
    /// <summary>Connection settings with the chosen proxy. Redirects are followed by hand so cookies never leave the original host.</summary>
    public static SocketsHttpHandler Handler(ProxySettings p)
    {
        var h = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
            MaxConnectionsPerServer = 32,
            ConnectTimeout = TimeSpan.FromSeconds(30),
        };
        var host = p.Host.Trim();
        switch (p.Mode)
        {
            case ProxyMode.None:
                h.UseProxy = false;
                break;
            case ProxyMode.Http or ProxyMode.Socks when host.Length > 0:
                var scheme = p.Mode == ProxyMode.Socks ? "socks5" : "http";
                var proxy = new WebProxy(new Uri($"{scheme}://{host}:{p.Port}"));
                if (p.Username.Length > 0) proxy.Credentials = new NetworkCredential(p.Username, p.Password);
                h.Proxy = proxy;
                h.UseProxy = true;
                break;
        }
        return h;
    }
}
