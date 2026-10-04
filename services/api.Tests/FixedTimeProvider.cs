namespace ogarniamy_zwierzaki_api.Tests;

// A clock the test sets, replacing TimeProvider.System in a test host.
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
