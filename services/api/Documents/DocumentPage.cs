namespace ogarniamy_zwierzaki_api.Documents;

// One page of an animal's Stored documents. HasMore tells whether a later page exists.
public sealed record DocumentPage(IReadOnlyList<DocumentSummary> Items, bool HasMore);
