namespace Nexonic.Core;

/// <summary>Resolves HTTP descriptors to bounded bytes; never treats a descriptor as completed torrent content.</summary>
public static class TorrentSource
{
    public const int MaximumMetadataBytes = 8 * 1024 * 1024;
    public static async Task<byte[]> ReadMetadataAsync(string source, CancellationToken cancellation = default)
    {
        Stream input;
        HttpResponseMessage? response = null;
        using var handler = Network.Handler(new ProxySettings { Mode = ProxyMode.None });
        handler.UseCookies = false;
        handler.Credentials = null;
        using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        try
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
            {
                response = await Network.SendAsync(http, uri, u => new HttpRequestMessage(HttpMethod.Get, u), HttpCompletionOption.ResponseHeadersRead, cancellation);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaximumMetadataBytes) throw new InvalidDataException("Torrent metadata exceeds 8 MiB.");
                input = await response.Content.ReadAsStreamAsync(cancellation);
            }
            else
            {
                if (!Path.IsPathFullyQualified(source) || !source.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Choose a local .torrent file or an HTTP(S) torrent link.");
                input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
            }
            await using (input)
            using (var output = new MemoryStream())
            {
                var buffer = new byte[81920];
                int count;
                while ((count = await input.ReadAsync(buffer, cancellation)) > 0)
                {
                    if (output.Length + count > MaximumMetadataBytes) throw new InvalidDataException("Torrent metadata exceeds 8 MiB.");
                    await output.WriteAsync(buffer.AsMemory(0, count), cancellation);
                }
                return output.ToArray(); // Native parsing validates bencoding, paths, hashes and file count.
            }
        }
        finally { response?.Dispose(); }
    }
}
