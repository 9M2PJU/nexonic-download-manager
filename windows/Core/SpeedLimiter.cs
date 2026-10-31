namespace Nexonic.Core;

/// <summary>Token bucket shared by every transfer. 0 bytes per second means no limit.</summary>
public sealed class SpeedLimiter
{
    readonly object gate = new();
    double rate, tokens;
    DateTime last = DateTime.UtcNow;

    public SpeedLimiter(int bytesPerSecond = 0) => SetLimit(bytesPerSecond);

    public void SetLimit(int bytesPerSecond)
    {
        lock (gate) { rate = Math.Max(0, bytesPerSecond); tokens = rate; last = DateTime.UtcNow; }
    }

    /// <summary>Takes bytes from the bucket and returns how long the caller should wait.</summary>
    public TimeSpan Consume(int bytes, DateTime? now = null)
    {
        lock (gate)
        {
            if (rate <= 0) return TimeSpan.Zero;
            var t = now ?? DateTime.UtcNow;
            tokens = Math.Min(rate, tokens + (t - last).TotalSeconds * rate);
            last = t;
            tokens -= bytes;
            return tokens >= 0 ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Min(5, -tokens / rate));
        }
    }
}
