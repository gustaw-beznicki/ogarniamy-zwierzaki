namespace ogarniamy_zwierzaki_api.Animals;

// An animal as its owner sees it. Version is the opaque value a client echoes in If-Match to edit the name or activity;
// StoredDocumentCount counts Stored documents only (an upload in progress is not counted).
public sealed record OwnedAnimal(Guid Id, string Name, bool IsActive, Guid Version, int StoredDocumentCount);
