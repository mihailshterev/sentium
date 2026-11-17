using System.Text;

namespace Sentium.Praxis.Features.Files;

/// <summary>
/// Cuts a line range out of a file's text under a character budget, so agents and previews page through large files
/// instead of loading them whole.
/// </summary>
internal static class TextWindow
{
    public static TextSlice Slice(string text, int startLine, int maxLines, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = text.ReplaceLineEndings("\n").Split('\n');
        var totalLines = lines.Length;
        var first = Math.Clamp(startLine, 1, Math.Max(totalLines, 1));
        var limit = Math.Max(maxLines, 1);
        var budget = Math.Max(maxChars, 1);

        var slice = new StringBuilder();
        var last = first - 1;

        for (var i = first - 1; i < totalLines && last - first + 1 < limit; i++)
        {
            var line = lines[i];
            var needed = line.Length + (slice.Length > 0 ? 1 : 0);

            if (slice.Length + needed > budget)
            {
                if (slice.Length == 0)
                {
                    // A single line longer than the budget is cut rather than skipped, so the window always advances.
                    slice.Append(line, 0, budget);
                    last = i + 1;
                }

                break;
            }

            if (slice.Length > 0)
            {
                slice.Append('\n');
            }

            slice.Append(line);
            last = i + 1;
        }

        return new TextSlice(slice.ToString(), first, last, totalLines);
    }
}
