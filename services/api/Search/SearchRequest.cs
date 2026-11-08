namespace ogarniamy_zwierzaki_api.Search;

// The body of POST /api/search. The query is validated by OwnedSearch.TryNormalizeQuery.
public sealed record SearchRequest(string? Query);
