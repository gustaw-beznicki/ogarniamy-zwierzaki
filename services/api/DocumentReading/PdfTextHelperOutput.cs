namespace ogarniamy_zwierzaki_api.DocumentReading;

// What the PDF parser child process writes to its standard output, as JSON. Status is one of the constants below;
// Pages is set only when it is Ok.
public sealed class PdfTextHelperOutput
{
    public const string Ok = "ok";
    public const string Encrypted = "encrypted";
    public const string PageLimit = "page_limit";
    public const string Malformed = "malformed";

    public string Status { get; init; } = Malformed;

    public List<PdfLocalPage>? Pages { get; init; }
}
