using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace ogarniamy_zwierzaki_api.Tests;

// Synthetic, non-sensitive PDFs for reading tests, generated with PdfPig's writer: text pages, scanned (image-only)
// pages, blank pages and any mix of them, in page order. The password-protected PDF is a committed 3 KB fixture made
// with Ghostscript from one line of synthetic text:
// gs -dNOPAUSE -dBATCH -sDEVICE=pdfwrite -sOwnerPassword=synthetic-owner -sUserPassword=synthetic-user
//    -dEncryptionR=3 -dKeyLength=128 -sOutputFile=password-protected.pdf <one-line PostScript page>
public static class ReadingFixtures
{
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Reading");

    public static byte[] SyntheticNotePng() => File.ReadAllBytes(Path.Combine(FixtureDirectory, "synthetic-note.png"));

    public static byte[] PasswordProtectedPdf() => File.ReadAllBytes(Path.Combine(FixtureDirectory, "password-protected.pdf"));

    public static byte[] Pdf(params PdfPageSpec[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        byte[]? image = null;
        foreach (var spec in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            foreach (var (text, x, y) in spec.Lines)
            {
                page.AddText(text, 14, new PdfPoint(x, y), font);
            }

            if (spec.Scanned)
            {
                image ??= SyntheticNotePng();
                page.AddPng(image, new PdfRectangle(72, 200, 472, 600));
            }
        }

        return builder.Build();
    }

    public static TestOriginal PdfOriginal(byte[] bytes, string name = "reading.pdf") => new(name, "application/pdf", bytes);
}
