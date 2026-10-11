namespace ogarniamy_zwierzaki_api.DocumentReading;

// The claim the worker is processing, the signal that its lease is gone, and the extracted-text budget it has used.
public sealed class DocumentReadingJobContext(DocumentReadingClaim claim, CancellationTokenSource leaseLost)
{
    private long _characters;

    public DocumentReadingClaim Claim { get; } = claim;

    public CancellationTokenSource LeaseLost { get; } = leaseLost;

    // Counts the pages' text against the per-document limit; exceeding it is a terminal failure, never truncation.
    public void Charge(IEnumerable<ReadPageResult> pages)
    {
        _characters += pages.Sum(page => (long)page.Text.Length);
        if (_characters > DocumentReadingWorker.MaxCharacters)
        {
            throw DocumentReadFailure.Terminal(DocumentReadFailure.TextLimitExceeded);
        }
    }
}
