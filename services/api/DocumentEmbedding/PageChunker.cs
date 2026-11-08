using ogarniamy_zwierzaki_api.DocumentReading;

namespace ogarniamy_zwierzaki_api.DocumentEmbedding;

// Cuts published page text into overlapping chunks for embedding. Pure and deterministic: the same pages always give
// the same chunks, ordered by file position, page and offset. A chunk never crosses a page; a blank page gives none; a
// page up to TargetLength characters is one chunk. Longer pages are cut at about TargetLength characters, preferring a
// line break, then any whitespace, then a hard cut (a very long line), and the next chunk starts about OverlapLength
// characters before the previous one ended, again on a line or word boundary where there is one.
public static class PageChunker
{
    // Bumped when the cutting rules change; part of the embedding version.
    public const int Version = 1;

    public const int TargetLength = 1200;

    // About 15% of the target.
    public const int OverlapLength = 180;

    // A cut is searched for in the second half of the window only, so chunks do not shrink to a few lines.
    private const int MinimumLength = TargetLength / 2;

    public static IReadOnlyList<PageChunk> Chunk(IEnumerable<DocumentTextPage> pages)
    {
        ArgumentNullException.ThrowIfNull(pages);

        var chunks = new List<PageChunk>();
        foreach (var page in pages.OrderBy(p => p.FilePosition).ThenBy(p => p.SourcePageNumber))
        {
            foreach (var (start, end) in Ranges(page.Text))
            {
                chunks.Add(new PageChunk(
                    page.DocumentFileId, page.FilePosition, page.SourcePageNumber, start, end, page.Text[start..end]));
            }
        }

        return chunks;
    }

    // The [start, end) ranges of one page's text, without leading or trailing whitespace; none for a blank page.
    public static IReadOnlyList<(int Start, int End)> Ranges(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var ranges = new List<(int, int)>();
        var start = SkipWhitespace(text, 0);
        if (start == text.Length)
        {
            return ranges;
        }

        while (true)
        {
            var hardEnd = start + TargetLength;
            if (hardEnd >= text.Length)
            {
                ranges.Add((start, TrimEnd(text, start, text.Length)));
                return ranges;
            }

            var end = CutBefore(text, start, hardEnd);
            ranges.Add((start, TrimEnd(text, start, end)));

            var next = OverlapStart(text, start, end);
            start = SkipWhitespace(text, next);
            if (start >= text.Length)
            {
                return ranges;
            }
        }
    }

    // Where the chunk that began at start ends (exclusive): at the last line break in the window's second half, else
    // at the last whitespace there, else at the hard limit. Never between the halves of a surrogate pair.
    private static int CutBefore(string text, int start, int hardEnd)
    {
        var lowest = start + MinimumLength;
        for (var i = hardEnd; i > lowest; i--)
        {
            if (text[i] == '\n')
            {
                return i;
            }
        }

        for (var i = hardEnd; i > lowest; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i;
            }
        }

        return char.IsLowSurrogate(text[hardEnd]) ? hardEnd - 1 : hardEnd;
    }

    // Where the next chunk starts: about OverlapLength characters before end, moved forward to the next line start,
    // else the next word start, when one lies before end. Always after start, so every pass makes progress.
    private static int OverlapStart(string text, int start, int end)
    {
        var from = end - OverlapLength;
        for (var i = from; i < end; i++)
        {
            if (text[i] == '\n')
            {
                return i + 1;
            }
        }

        for (var i = from; i < end; i++)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i + 1;
            }
        }

        return char.IsLowSurrogate(text[from]) ? from - 1 : from;
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static int TrimEnd(string text, int start, int end)
    {
        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return end;
    }
}
