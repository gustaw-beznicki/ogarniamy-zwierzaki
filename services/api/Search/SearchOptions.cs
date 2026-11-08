using Microsoft.Extensions.Options;

namespace ogarniamy_zwierzaki_api.Search;

// The "Search" configuration section. Invalid values never stop the API (originals must stay available): search
// answers 503 search_unavailable and the startup log names the setting to fix.
public sealed class SearchOptions
{
    public const string SectionName = "Search";

    // The lowest cosine similarity (1 = same direction) at which a document counts as a match. Provisional until it is
    // calibrated against real documents; an absolute cut, never relative to the best result.
    public double MinSimilarity { get; init; } = 0.30;

    // Searches one account may send per minute (see SearchRateLimit).
    public int MaxRequestsPerMinute { get; init; } = 30;

    // The bound settings, or null with a reason (never the raw value) when the section cannot be bound or is out of
    // range.
    public static SearchOptions? TryRead(IOptions<SearchOptions> options, out string reason)
    {
        SearchOptions value;
        try
        {
            value = options.Value;
        }
        catch (InvalidOperationException)
        {
            reason = $"the {SectionName} section has an invalid value; check MinSimilarity and MaxRequestsPerMinute.";
            return null;
        }

        if (double.IsNaN(value.MinSimilarity) || value.MinSimilarity is < 0 or > 1)
        {
            reason = "Search:MinSimilarity must be a number between 0 and 1.";
            return null;
        }

        if (value.MaxRequestsPerMinute < 1)
        {
            reason = "Search:MaxRequestsPerMinute must be at least 1.";
            return null;
        }

        reason = $"minimum similarity {value.MinSimilarity}, {value.MaxRequestsPerMinute} searches per account per minute.";
        return value;
    }
}
