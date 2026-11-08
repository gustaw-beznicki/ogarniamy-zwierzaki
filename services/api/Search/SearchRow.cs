namespace ogarniamy_zwierzaki_api.Search;

// One row of the owner-scoped search query; column aliases are these property names in snake_case (the context's naming convention).
public sealed class SearchRow
{
    public Guid DocumentId { get; set; }

    public Guid AnimalId { get; set; }

    public string AnimalName { get; set; } = string.Empty;

    public DateOnly EventDate { get; set; }

    public Guid FileId { get; set; }

    public int FilePosition { get; set; }

    public int SourcePageNumber { get; set; }

    public int FilePageCount { get; set; }

    public string Fragment { get; set; } = string.Empty;
}
