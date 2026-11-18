using System.Text;

namespace Sentium.Memoria.Features.Parsing;

/// <summary>
/// Turns a note into embedding chunks along its headings. Every chunk starts with the note title and heading path
/// (<c>Title &gt; H1 &gt; H2</c>) so a chunk retrieved on its own still says where it came from; a section longer than
/// <see cref="MaxChunkChars"/> is split at paragraph, then line, then word boundaries.
/// </summary>
internal static class MarkdownNoteChunker
{
    public const int MaxChunkChars = 1_500;

    private const string PathSeparator = " > ";

    public static IReadOnlyList<string> Chunk(string title, IReadOnlyList<string> tags, IReadOnlyList<NoteSection> sections)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(sections);

        var chunks = new List<string>();

        foreach (var section in sections)
        {
            if (section.Text.Length == 0)
            {
                continue;
            }

            var header = string.Join(PathSeparator, [title, .. section.HeadingPath]);

            foreach (var piece in Split(section.Text, MaxChunkChars))
            {
                chunks.Add($"{header}\n\n{piece}");
            }
        }

        if (chunks.Count == 0)
        {
            // A note with only a title (or only headings) is still findable by name.
            chunks.Add(string.Join(PathSeparator, [title, .. sections.SelectMany(s => s.HeadingPath).Distinct()]));
        }

        if (tags.Count > 0)
        {
            chunks[0] = $"{chunks[0]}\n\nTags: {string.Join(", ", tags)}";
        }

        return chunks;
    }

    private static IEnumerable<string> Split(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            yield return text;
            yield break;
        }

        var buffer = new StringBuilder();

        foreach (var part in Pieces(text, maxChars))
        {
            if (buffer.Length > 0 && buffer.Length + part.Length + 2 > maxChars)
            {
                yield return buffer.ToString().Trim();
                buffer.Clear();
            }

            if (buffer.Length > 0)
            {
                buffer.Append("\n\n");
            }

            buffer.Append(part);
        }

        if (buffer.Length > 0)
        {
            yield return buffer.ToString().Trim();
        }
    }

    /// <summary>
    /// Paragraphs, with any paragraph still too long broken at lines and then at words.
    /// </summary>
    private static IEnumerable<string> Pieces(string text, int maxChars)
    {
        foreach (var paragraph in text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (paragraph.Length <= maxChars)
            {
                yield return paragraph;
                continue;
            }

            foreach (var line in paragraph.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.Length <= maxChars)
                {
                    yield return line;
                    continue;
                }

                foreach (var slice in SliceAtWords(line, maxChars))
                {
                    yield return slice;
                }
            }
        }
    }

    private static IEnumerable<string> SliceAtWords(string line, int maxChars)
    {
        var start = 0;
        while (start < line.Length)
        {
            var length = Math.Min(maxChars, line.Length - start);
            if (start + length < line.Length)
            {
                var space = line.LastIndexOf(' ', start + length - 1, length);
                if (space > start)
                {
                    length = space - start;
                }
            }

            yield return line.Substring(start, length).Trim();
            start += length;
        }
    }
}
