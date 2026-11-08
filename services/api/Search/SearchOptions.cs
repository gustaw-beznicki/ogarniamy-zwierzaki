namespace ogarniamy_zwierzaki_api.Search;

// The "Search" configuration section.
public sealed class SearchOptions
{
    public const string SectionName = "Search";

    // The lowest cosine similarity (1 = same direction) at which a document counts as a match. Provisional until it is
    // calibrated against real documents; an absolute cut, never relative to the best result.
    public double MinSimilarity { get; init; } = 0.30;
}
