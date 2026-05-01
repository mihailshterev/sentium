namespace AgentRuntime.Infrastructure.Rag;

/// <summary>
/// Splits a long string into overlapping fixed-size character windows.
/// A sentence-boundary heuristic is applied at each cut point to avoid
/// truncating mid-sentence wherever possible.
/// </summary>
public static class TextChunker
{
    /// <summary>
    /// Returns an ordered list of chunks produced from <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The source text to split.</param>
    /// <param name="chunkSize">Maximum characters per chunk (before overlap).</param>
    /// <param name="overlap">
    /// Characters of context carried forward from the end of one chunk into the next.
    /// Must be less than <paramref name="chunkSize"/>.
    /// </param>
    public static IReadOnlyList<string> Chunk(string text, int chunkSize = 500, int overlap = 50)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkSize);
        ArgumentOutOfRangeException.ThrowIfNegative(overlap);

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        if (text.Length <= chunkSize)
        {
            return [text.Trim()];
        }

        var chunks = new List<string>();
        var start = 0;

        while (start < text.Length)
        {
            var end = Math.Min(start + chunkSize, text.Length);

            if (end < text.Length)
            {
                var breakIdx = text.LastIndexOfAny(['.', '!', '?', '\n'], end - 1, end - start);
                if (breakIdx > start)
                {
                    end = breakIdx + 1;
                }
            }

            var chunk = text[start..end].Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            var nextStart = end - overlap;
            if (nextStart <= start)
            {
                nextStart = start + 1;
            }

            start = nextStart;
        }

        return chunks;
    }
}
