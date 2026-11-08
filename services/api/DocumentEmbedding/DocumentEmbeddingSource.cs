using ogarniamy_zwierzaki_api.DocumentReading;

namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// What one embedding run is computed from: the published text revision of a document and its animal's current name,
// read in one snapshot. Publishing is accepted only while both still match.
public sealed record DocumentEmbeddingSource(int RunGeneration, string AnimalName, IReadOnlyList<DocumentTextPage> Pages);
