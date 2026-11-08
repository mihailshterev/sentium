using System.Text.RegularExpressions;

namespace Sentium.Core.Text;

/// <summary>
/// Tags are free-form but stored in one canonical form (trimmed, single-spaced, lower-case, unique) so
/// "Power Tools", " power  tools" and "POWER TOOLS" are the same tag for filtering and autocomplete.
/// </summary>
public static partial class TagNormalizer
{
    public static string? NormalizeOne(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        return Whitespace().Replace(tag.Trim(), " ").ToLowerInvariant();
    }

    /// <summary>
    /// Normalizes and de-duplicates, keeping first-seen order and dropping blanks.
    /// </summary>
    public static List<string> Normalize(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (var tag in tags)
        {
            if (NormalizeOne(tag) is { } normalized && seen.Add(normalized))
            {
                result.Add(normalized);
            }
        }

        return result;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
