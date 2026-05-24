using System.Buffers;

namespace Sentium.AgentRuntime.Infrastructure.Rag;

/// <summary>
/// Splits a long string into overlapping fixed-size character windows.
/// A sentence-boundary heuristic is applied at each cut point to avoid
/// truncating mid-sentence wherever possible.
/// </summary>
public static class TextChunker
{
    private static readonly SearchValues<char> BoundaryChars = SearchValues.Create(['.', '!', '?', '\n']);

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

        if (overlap >= chunkSize)
        {
            throw new ArgumentException("Overlap must be less than chunk size.", nameof(overlap));
        }

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
        var textLength = text.Length;

        while (start < textLength)
        {
            var end = Math.Min(start + chunkSize, textLength);

            if (end < textLength)
            {
                var breakIdx = text.AsSpan(start, end - start).LastIndexOfAny(BoundaryChars);
                if (breakIdx >= 0)
                {
                    end = start + breakIdx + 1;
                }
            }

            var chunk = text.AsSpan(start, end - start).Trim().ToString();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            var nextStart = end - overlap;
            if (nextStart <= start)
            {
                nextStart = end;
            }

            start = nextStart;
        }

        return chunks;
    }
}
