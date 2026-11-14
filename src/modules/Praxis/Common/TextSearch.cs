using System.Text;

namespace Sentium.Praxis.Common;

/// <summary>
/// Helpers for the case-insensitive "contains" filters behind list search boxes (<c>EF.Functions.ILike</c>).
/// </summary>
internal static class TextSearch
{
    /// <summary>
    /// Escape character passed to <c>ILike</c> together with <see cref="ToContainsPattern"/>.
    /// </summary>
    public const string EscapeCharacter = "\\";

    /// <summary>
    /// Longest search term honoured; anything longer is cut.
    /// </summary>
    public const int MaxTermLength = 200;

    /// <summary>
    /// The trimmed term, cut to <see cref="MaxTermLength"/>, or <see langword="null"/> when there is nothing to search for.
    /// </summary>
    public static string? Normalize(string? term)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return null;
        }

        var trimmed = term.Trim();
        return trimmed.Length > MaxTermLength ? trimmed[..MaxTermLength] : trimmed;
    }

    /// <summary>
    /// <c>%term%</c> with the LIKE wildcards in <paramref name="term"/> escaped, so "100%" or "a_b" match literally.
    /// </summary>
    public static string ToContainsPattern(string term)
    {
        ArgumentNullException.ThrowIfNull(term);

        var builder = new StringBuilder(term.Length + 2).Append('%');
        foreach (var c in term)
        {
            if (c is '%' or '_' or '\\')
            {
                builder.Append('\\');
            }

            builder.Append(c);
        }

        return builder.Append('%').ToString();
    }
}
