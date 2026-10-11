using System.Text.Encodings.Web;
using System.Text.Json;
using ogarniamy_zwierzaki_api.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
using UglyToad.PdfPig.Exceptions;

namespace ogarniamy_zwierzaki_api.DocumentReading;

// The PDF parser child process: `ogarniamy-zwierzaki-api pdf-text-helper` reads one PDF from standard input and
// writes a PdfTextHelperOutput as JSON to standard output. It is the API's own assembly started in a hidden mode
// (Program.cs, before any configuration, database or storage is set up), so it ships with the API. Running it in a
// separate process lets PdfTextReader kill CPU-bound or runaway parsing without affecting the API. It never writes
// files and never logs; the text goes only to the parent through the pipe.
public static class PdfTextReaderProcess
{
    public const string Command = "pdf-text-helper";

    // The most pages read from one PDF; a larger one fails reading instead of being truncated.
    public const int MaxPages = 2_000;

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Non-ASCII text stays as UTF-8 instead of \uXXXX escapes, keeping the output compact.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static bool IsRequested(string[] args) => args.Length == 1 && args[0] == Command;

    // Entry point from Program.cs. Returns the process exit code; the outcome itself is in the JSON.
    public static async Task<int> RunAsync(Stream input, Stream output)
    {
        var result = Parse(await ReadInputAsync(input));
        await JsonSerializer.SerializeAsync(output, result, JsonOptions);
        await output.FlushAsync();
        return 0;
    }

    // Null when the input is larger than any stored original can be.
    private static async Task<byte[]?> ReadInputAsync(Stream input)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await input.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + read > DocumentFile.MaxByteLength)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static PdfTextHelperOutput Parse(byte[]? bytes)
    {
        if (bytes is null || bytes.Length == 0)
        {
            return new PdfTextHelperOutput { Status = PdfTextHelperOutput.Malformed };
        }

        try
        {
            using var document = PdfDocument.Open(bytes, new ParsingOptions { UseLenientParsing = true, SkipMissingFonts = true });
            if (document.NumberOfPages > MaxPages)
            {
                return new PdfTextHelperOutput { Status = PdfTextHelperOutput.PageLimit };
            }

            var pages = new List<PdfLocalPage>(document.NumberOfPages);
            for (var number = 1; number <= document.NumberOfPages; number++)
            {
                var page = document.GetPage(number);
                pages.Add(new PdfLocalPage(number, LayoutText(page), page.GetImages().Any()));
            }

            return new PdfTextHelperOutput { Status = PdfTextHelperOutput.Ok, Pages = pages };
        }
        catch (PdfDocumentEncryptedException)
        {
            return new PdfTextHelperOutput { Status = PdfTextHelperOutput.Encrypted };
        }
        catch (Exception)
        {
            // Any parser fault on untrusted input; the parent falls back to the provider.
            return new PdfTextHelperOutput { Status = PdfTextHelperOutput.Malformed };
        }
    }

    // Layout-aware text: letters grouped into words by proximity, words into blocks by recursive XY cuts, which
    // yields blocks in reading order (top to bottom, columns left to right). Unlike page.Text, the result does not
    // depend on the order of drawing operations.
    private static string LayoutText(UglyToad.PdfPig.Content.Page page)
    {
        var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters).ToList();
        if (words.Count == 0)
        {
            return string.Empty;
        }

        return string.Join("\n\n", RecursiveXYCut.Instance.GetBlocks(words).Select(block => block.Text));
    }
}
