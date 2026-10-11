namespace ogarniamy_zwierzaki_api.DocumentReading;

// How the text of a published page was obtained.
public enum DocumentTextExtractionMethod
{
    // The PDF's own text layer, read locally.
    TextLayer,

    // Optical character recognition by the provider.
    Ocr,
}
