namespace ogarniamy_zwierzaki_api.Tests;

// One page of a synthetic PDF (ReadingFixtures.Pdf): text lines with their start point, in drawing order, and whether a
// scanned image covers the page.
public sealed record PdfPageSpec(IReadOnlyList<(string Text, double X, double Y)> Lines, bool Scanned)
{
    // Lines drawn top to bottom.
    public static PdfPageSpec Text(params string[] lines) =>
        new(lines.Select((line, index) => (line, 72.0, 720.0 - 40 * index)).ToList(), false);

    public static PdfPageSpec Scan() => new([], true);

    public static PdfPageSpec Blank() => new([], false);
}
