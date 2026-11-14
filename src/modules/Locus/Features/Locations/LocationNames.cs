using System.Text.RegularExpressions;

namespace Sentium.Locus.Features.Locations;

internal static partial class LocationNames
{
    /// <summary>
    /// Trims and collapses internal whitespace so "Shelf  B " and "Shelf B" are the same name.
    /// </summary>
    public static string Clean(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Whitespace().Replace(name.Trim(), " ");
    }

    /// <summary>
    /// The case-folded form used by the unique sibling-name index.
    /// </summary>
    public static string Normalize(string name) => Clean(name).ToLowerInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
