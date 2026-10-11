namespace ogarniamy_zwierzaki_api.DocumentReading;

// What the local parser made of a PDF, saved on the run file that sends it to OCR (before the submission), so later
// attempts of the same run generation reuse it instead of parsing the PDF again. Pages is null when the parser could
// not read the file (including a timeout): later attempts then go straight to whole-file OCR.
public sealed record PdfLocalCheckpoint(IReadOnlyList<PdfLocalPage>? Pages);
