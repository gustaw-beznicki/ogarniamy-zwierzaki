namespace ogarniamy_zwierzaki_api.DocumentReading;

// Wakes the idle worker when new reading work was committed in this process (a completed capture or an accepted owner
// Retry), so the idle poll can back off without delaying fresh work. Only a hint: work committed elsewhere (another
// instance, a migration, a lease expiry) is still found by the next poll.
public sealed class DocumentReadingWakeUp
{
    private readonly SemaphoreSlim signal = new(0, 1);

    public void Notify()
    {
        try
        {
            signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signalled; one pending wake-up is enough.
        }
    }

    // Waits for a wake-up or the timeout. True when woken.
    public async Task<bool> WaitAsync(TimeSpan timeout, TimeProvider time, CancellationToken cancellationToken)
    {
        using var timer = new CancellationTokenSource(timeout, time);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timer.Token);
        try
        {
            await signal.WaitAsync(wait.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
