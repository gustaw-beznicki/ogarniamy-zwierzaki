using System.Text;

namespace ogarniamy_zwierzaki_api.Tests;

// Small, well-formed and non-sensitive originals that real viewers can open: a 4x3 PNG, a 4x3 JPEG and a text-only
// PDF with any number of pages. TestOriginal covers generated bodies of exact sizes.
public static class SampleOriginals
{
    private const string PngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAADCAIAAAA7ljmRAAAAGklEQVR42k3GIQEAAADDIPqX3u1RCKF+ccsAIhMO8vRgKloAAAAASUVORK5CYII=";

    private const string JpegBase64 =
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYU" +
        "GBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAADAAQD" +
        "ASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKB" +
        "kaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZ" +
        "mqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQF" +
        "BgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5" +
        "OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX" +
        "2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwDT+HUt1F4ZsPsupanpgm0/TLuVNM1CezWWafTLSeV3WJ1DEvKwGc7UWONdscaIpRRX" +
        "8g8R/wDI4xX+OX5n8tcc8X8R5XxBiMHgMyr0qUFT5YQq1IxX7uD0jGSS1bei3P/Z";

    public static TestOriginal Png(string name = "sample.png") =>
        new(name, "image/png", Convert.FromBase64String(PngBase64));

    public static TestOriginal Jpeg(string name = "sample.jpg") =>
        new(name, "image/jpeg", Convert.FromBase64String(JpegBase64));

    // A PDF 1.7 file with one line of text per page and a correct cross-reference table.
    public static TestOriginal Pdf(int pages, string name = "sample.pdf")
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pages, 1);

        // Objects: 1 catalog, 2 page tree, 3 font, then a page and its content stream for every page.
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            $"<< /Type /Pages /Count {pages} /Kids [{string.Join(' ', Enumerable.Range(0, pages).Select(p => $"{4 + 2 * p} 0 R"))}] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        for (var page = 0; page < pages; page++)
        {
            var text = $"BT /F1 24 Tf 72 720 Td (Sample page {page + 1}) Tj ET";
            objects.Add(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {5 + 2 * page} 0 R >>");
            objects.Add($"<< /Length {text.Length} >>\nstream\n{text}\nendstream");
        }

        var pdf = new StringBuilder("%PDF-1.7\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append($"{offset:D10} 00000 n \n");
        }

        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        // Plain ASCII, so character offsets are byte offsets.
        return new TestOriginal(name, "application/pdf", Encoding.ASCII.GetBytes(pdf.ToString()));
    }
}
