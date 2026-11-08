using ogarniamy_zwierzaki_api.DocumentEmbedding;

namespace ogarniamy_zwierzaki_api.Tests;

// The worker's token bucket: one minute's allowance at once, then the refill rate.
public sealed class EmbeddingTokenPacerTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_full_bucket_grants_one_minutes_allowance_without_waiting()
    {
        var pacer = new EmbeddingTokenPacer(40_000, new FixedTimeProvider(Start));

        Assert.Equal(TimeSpan.Zero, pacer.Reserve(16_000));
        Assert.Equal(TimeSpan.Zero, pacer.Reserve(16_000));
        Assert.Equal(TimeSpan.Zero, pacer.Reserve(8_000));
    }

    [Fact]
    public void Tokens_beyond_the_allowance_wait_in_proportion_to_the_rate()
    {
        var pacer = new EmbeddingTokenPacer(40_000, new FixedTimeProvider(Start));
        pacer.Reserve(40_000);

        Assert.Equal(TimeSpan.FromSeconds(15), pacer.Reserve(10_000));
        Assert.Equal(TimeSpan.FromSeconds(30), pacer.Reserve(10_000));
    }

    [Fact]
    public void The_bucket_refills_over_time_up_to_one_minutes_allowance()
    {
        var clock = new FixedTimeProvider(Start);
        var pacer = new EmbeddingTokenPacer(40_000, clock);
        pacer.Reserve(40_000);

        clock.Now = Start.AddSeconds(30);
        Assert.Equal(TimeSpan.Zero, pacer.Reserve(20_000));

        clock.Now = Start.AddHours(1);
        Assert.Equal(TimeSpan.Zero, pacer.Reserve(40_000));
        Assert.Equal(TimeSpan.FromSeconds(15), pacer.Reserve(10_000));
    }

    [Fact]
    public void Zero_tokens_per_minute_turns_pacing_off()
    {
        var pacer = new EmbeddingTokenPacer(0, new FixedTimeProvider(Start));

        Assert.Equal(TimeSpan.Zero, pacer.Reserve(1_000_000));
    }
}
