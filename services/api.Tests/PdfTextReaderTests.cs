using ogarniamy_zwierzaki_api.DocumentReading;

namespace ogarniamy_zwierzaki_api.Tests;

// The local PDF reader with its real child process: layout order, which pages count as usable, raster detection,
// failure classification, the parse timeout and the output cap. No containers and no provider.
public sealed class PdfTextReaderTests
{
    [Fact]
    public async Task Text_comes_in_layout_reading_order_not_drawing_order()
    {
        // Drawn bottom-up and right column first: raw content order would be the reverse of reading order.
        var pdf = ReadingFixtures.Pdf(new PdfPageSpec(
            [
                ("Right column text", 320, 300), ("Left column text", 72, 300),
                ("Second line of the synthetic note", 72, 520), ("First line of the synthetic note", 72, 720),
            ],
            false));

        var page = Assert.Single(await ReadAsync(pdf));

        Assert.Equal(1, page.PageNumber);
        Assert.False(page.HasRasterImage);
        var positions = new[] { "First line", "Second line", "Left column", "Right column" }
            .Select(text => page.Text.IndexOf(text, StringComparison.Ordinal))
            .ToList();
        Assert.True(!positions.Contains(-1), page.Text);
        Assert.Equal(positions.Order(), positions);
        Assert.True(PdfTextReader.IsUsable(page));
    }

    [Fact]
    public async Task Every_page_is_returned_in_order_with_scanned_and_blank_pages_marked_for_ocr()
    {
        var pdf = ReadingFixtures.Pdf(
            PdfPageSpec.Text("Vaccination record page one"),
            PdfPageSpec.Scan(),
            new PdfPageSpec([("Caption above a scan", 72, 760)], true),
            PdfPageSpec.Blank());

        var pages = await ReadAsync(pdf);

        Assert.Equal([1, 2, 3, 4], pages.Select(p => p.PageNumber));
        Assert.Equal([true, false, false, false], pages.Select(PdfTextReader.IsUsable));
        Assert.Equal([false, true, true, false], pages.Select(p => p.HasRasterImage));
        Assert.Contains("Caption above a scan", pages[2].Text);
        Assert.Equal(string.Empty, pages[3].Text);
    }

    [Theory]
    [InlineData("Rabies 2026", true)]
    [InlineData("   \n\t ", false)]
    [InlineData("-- . --", false)]
    [InlineData("Broken � glyph", false)]
    [InlineData("Null \0 inside", false)]
    [InlineData("Control \u0007 bell", false)]
    [InlineData("Lines\r\nwith whitespace 7", true)]
    public void A_page_is_usable_with_a_letter_or_digit_and_no_broken_characters(string text, bool usable) =>
        Assert.Equal(usable, PdfTextReader.IsUsable(new PdfLocalPage(1, text, false)));

    [Fact]
    public async Task A_malformed_pdf_falls_back_to_the_provider()
    {
        var failure = await Assert.ThrowsAsync<DocumentReadFailure>(() => ReadAsync(TestOriginal.Pdf(4_000).Bytes));

        Assert.True(failure.CanFallBackToProvider);
        Assert.Equal(DocumentReadFailure.PdfUnreadable, failure.Code);
    }

    [Fact]
    public async Task A_password_protected_pdf_fails_for_good()
    {
        var failure = await Assert.ThrowsAsync<DocumentReadFailure>(() => ReadAsync(ReadingFixtures.PasswordProtectedPdf()));

        Assert.Equal(DocumentReadFailure.EncryptedPdf, failure.Code);
        Assert.False(failure.IsTransient);
        Assert.False(failure.CanFallBackToProvider);
    }

    [Fact]
    public async Task More_than_the_page_limit_fails_for_good_instead_of_truncating()
    {
        var pdf = ReadingFixtures.Pdf(Enumerable.Range(0, PdfTextReaderProcess.MaxPages + 1).Select(_ => PdfPageSpec.Blank()).ToArray());

        var failure = await Assert.ThrowsAsync<DocumentReadFailure>(() => ReadAsync(pdf));

        Assert.Equal(DocumentReadFailure.PageLimitExceeded, failure.Code);
        Assert.False(failure.IsTransient);
    }

    [Fact]
    public async Task A_parse_that_outlives_the_timeout_is_killed_and_falls_back()
    {
        var reader = new PdfTextReader { Timeout = TimeSpan.FromMilliseconds(1) };

        var failure = await Assert.ThrowsAsync<DocumentReadFailure>(
            () => reader.ReadAsync(new MemoryStream(ReadingFixtures.Pdf(PdfPageSpec.Text("Slow"))), CancellationToken.None));

        Assert.True(failure.CanFallBackToProvider);
    }

    [Fact]
    public async Task Output_beyond_the_cap_is_refused()
    {
        var reader = new PdfTextReader { MaxOutputBytes = 16 };

        var failure = await Assert.ThrowsAsync<DocumentReadFailure>(
            () => reader.ReadAsync(new MemoryStream(ReadingFixtures.Pdf(PdfPageSpec.Text("Too long for the cap"))), CancellationToken.None));

        Assert.True(failure.CanFallBackToProvider);
    }

    [Fact]
    public async Task Cancellation_by_the_caller_is_not_reported_as_a_parser_failure()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new PdfTextReader().ReadAsync(new MemoryStream(ReadingFixtures.Pdf(PdfPageSpec.Text("x"))), cancelled.Token));
    }

    private static Task<IReadOnlyList<PdfLocalPage>> ReadAsync(byte[] pdf) =>
        new PdfTextReader().ReadAsync(new MemoryStream(pdf), CancellationToken.None);
}
