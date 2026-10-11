namespace ogarniamy_zwierzaki_api.DocumentReading;

// The state of a provider analysis: still running (poll again after RetryAfter), or complete with its pages in the
// provider's order. Page numbers are those of the original.
public sealed record OcrAnalysis(bool IsComplete, TimeSpan? RetryAfter, IReadOnlyList<ReadPageResult> Pages)
{
    public static OcrAnalysis Running(TimeSpan? retryAfter) => new(false, retryAfter, []);
}
