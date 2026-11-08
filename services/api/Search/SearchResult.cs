namespace ogarniamy_zwierzaki_api.Search;

// One matching document with the single fragment that matched best. OriginalUrl opens the original that holds the
// fragment. There is deliberately no score: the product shows sources, never a ranking number or a conclusion.
public sealed record SearchResult(
    Guid DocumentId,
    Guid AnimalId,
    string AnimalName,
    bool AnimalIsActive,
    DateOnly EventDate,
    Guid FileId,
    int FilePosition,
    int SourcePageNumber,
    int FilePageCount,
    string Fragment,
    string OriginalUrl);
