namespace ogarniamy_zwierzaki_api.Documents;

// A Stored original ready to stream. The caller owns Content and must dispose it.
public sealed record OpenedOriginal(DocumentFileDetails File, Stream Content);
