namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// Keeps the background worker's estimated token use under a per-minute rate, so a backfill or a long document does not
// run into the deployment's tokens-per-minute limit and leaves room for searches. A token bucket that holds one
// minute's allowance, starts full and refills continuously: short bursts up to that allowance go out at once, anything
// beyond waits for the refill. Not thread-safe; the worker processes one job at a time.
public sealed class EmbeddingTokenPacer
{
    private readonly int tokensPerMinute;
    private readonly TimeProvider time;
    private double available;
    private DateTimeOffset refilledAt;

    // tokensPerMinute 0 turns pacing off: every reservation is granted at once.
    public EmbeddingTokenPacer(int tokensPerMinute, TimeProvider time)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(tokensPerMinute);
        ArgumentNullException.ThrowIfNull(time);
        this.tokensPerMinute = tokensPerMinute;
        this.time = time;
        available = tokensPerMinute;
        refilledAt = time.GetUtcNow();
    }

    // Takes the tokens from the bucket and returns how long to wait before sending them (zero when they fit now). A
    // reservation larger than the whole bucket is allowed and simply waits longer.
    public TimeSpan Reserve(long estimatedTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(estimatedTokens);
        if (tokensPerMinute == 0)
        {
            return TimeSpan.Zero;
        }

        Refill();
        available -= estimatedTokens;
        return available >= 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(-available / tokensPerMinute);
    }

    // How long sending this many tokens from now would wait in total, without reserving them. The worker uses it to give
    // a long document's attempt enough time for its pacing waits.
    public TimeSpan EstimateWait(long estimatedTokens)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(estimatedTokens);
        if (tokensPerMinute == 0)
        {
            return TimeSpan.Zero;
        }

        Refill();
        var remaining = available - estimatedTokens;
        return remaining >= 0 ? TimeSpan.Zero : TimeSpan.FromMinutes(-remaining / tokensPerMinute);
    }

    private void Refill()
    {
        var now = time.GetUtcNow();
        if (now > refilledAt)
        {
            available = Math.Min(tokensPerMinute, available + (now - refilledAt).TotalMinutes * tokensPerMinute);
            refilledAt = now;
        }
    }
}
