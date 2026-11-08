namespace ogarniamy_zwierzaki_api.Search;

// Items are ordered by relevance, at most OwnedSearch.MaxResults. NotSearchableCount is how many of the owner's stored
// documents cannot be found yet because their text is not read or not embedded; failed readings are not counted.
public sealed record SearchResponse(IReadOnlyList<SearchResult> Items, int NotSearchableCount);
