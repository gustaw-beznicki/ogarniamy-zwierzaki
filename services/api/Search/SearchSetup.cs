using Microsoft.Extensions.Options;

namespace ogarniamy_zwierzaki_api.Search;

// Startup reporting for search, next to the reading and embedding configuration lines.
public static class SearchSetup
{
    // One startup line with the search settings, or a warning naming the invalid one (search then answers 503).
    public static void LogSearchConfiguration(this WebApplication app)
    {
        var settings = SearchOptions.TryRead(app.Services.GetRequiredService<IOptions<SearchOptions>>(), out var reason);
        if (settings is null)
        {
            app.Logger.LogWarning("Search is unavailable: {Reason} Documents and originals are not affected.", reason);
        }
        else
        {
            app.Logger.LogInformation("Search settings: {Settings}", reason);
        }
    }
}
