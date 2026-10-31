using System.Text.RegularExpressions;

namespace Nexonic.Core;

public static class LinkExtractor
{
    static readonly Regex Attr = new(@"(?:href|src)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>""']+))", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Absolute web links found in href and src attributes, in page order, without duplicates or #fragments.</summary>
    public static List<Uri> Links(string html, Uri baseUri)
    {
        var seen = new HashSet<string>();
        var result = new List<Uri>();
        foreach (Match m in Attr.Matches(html))
        {
            var raw = (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value)
                .Replace("&amp;", "&").Trim();
            if (raw.Length == 0 || raw.StartsWith('#') || !Uri.TryCreate(baseUri, raw, out var u)) continue;
            if (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps) continue;
            var clean = new UriBuilder(u) { Fragment = "" }.Uri;
            if (seen.Add(clean.AbsoluteUri)) result.Add(clean);
        }
        return result;
    }
}

public sealed class GrabOptions
{
    public required Uri Start { get; init; }
    public int Depth { get; init; } = 1;
    public HashSet<string> Types { get; init; } = new(); // empty = every file that is not a web page
    public bool StayInFolder { get; init; } = true;
    public int MaxPages { get; init; } = 300;
}

/// <summary>Explores a section of a website and lists the files it links to.</summary>
public static class SiteGrabber
{
    static readonly HashSet<string> PageTypes = new() { "", "html", "htm", "php", "asp", "aspx", "jsp", "cgi", "shtml" };

    static string Ext(Uri u) => Path.GetExtension(u.AbsolutePath).TrimStart('.').ToLowerInvariant();
    public static bool IsPage(Uri u) => PageTypes.Contains(Ext(u));

    public static bool InScope(Uri u, GrabOptions o)
    {
        if (!string.Equals(u.Host, o.Start.Host, StringComparison.OrdinalIgnoreCase)) return false;
        if (!o.StayInFolder) return true;
        var folder = o.Start.AbsolutePath;
        folder = folder[..(folder.LastIndexOf('/') + 1)];
        return u.AbsolutePath.StartsWith(folder, StringComparison.Ordinal) || u.AbsolutePath + "/" == folder;
    }

    public static bool Wanted(Uri u, GrabOptions o) => o.Types.Count == 0 ? !PageTypes.Contains(Ext(u)) : o.Types.Contains(Ext(u));

    public static async Task<List<Uri>> ExploreAsync(GrabOptions o, HttpClient http, Action<int, int>? progress = null, CancellationToken ct = default)
    {
        var depth = Math.Clamp(o.Depth, 0, 5);
        var queue = new Queue<(Uri Page, int Level)>();
        queue.Enqueue((o.Start, 0));
        var visited = new HashSet<string>();
        var found = new HashSet<string>();
        var files = new List<Uri>();
        while (queue.Count > 0 && visited.Count < o.MaxPages && !ct.IsCancellationRequested)
        {
            var (page, level) = queue.Dequeue();
            if (!visited.Add(page.AbsoluteUri)) continue;
            progress?.Invoke(visited.Count, files.Count);
            string html;
            Uri final;
            try
            {
                using var res = await http.GetAsync(page, HttpCompletionOption.ResponseHeadersRead, ct);
                var type = res.Content.Headers.ContentType?.MediaType ?? "text/html";
                if (!res.IsSuccessStatusCode || !type.Contains("html") || res.Content.Headers.ContentLength > 5 << 20) continue;
                html = await res.Content.ReadAsStringAsync(ct);
                final = res.RequestMessage?.RequestUri ?? page;
            }
            catch (Exception) when (!ct.IsCancellationRequested) { continue; }
            foreach (var link in LinkExtractor.Links(html, final))
            {
                if (Wanted(link, o)) { if (found.Add(link.AbsoluteUri)) files.Add(link); }
                else if (IsPage(link) && level < depth && InScope(link, o) && !visited.Contains(link.AbsoluteUri)) queue.Enqueue((link, level + 1));
            }
        }
        return files;
    }
}
