namespace ogarniamy_zwierzaki_api.DocumentReading;

// One page of a PDF as the local parser saw it: its text layer in layout reading order, and whether the page draws
// raster images (which may hold scanned text that the text layer lacks).
public sealed record PdfLocalPage(int PageNumber, string Text, bool HasRasterImage);
