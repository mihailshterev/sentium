namespace Sentium.Memoria.Common;

internal static class SqlLike
{
    public const string EscapeCharacter = "\\";

    /// <summary>
    /// A case-insensitive <c>ILIKE</c> pattern matching <paramref name="text"/> anywhere, with its wildcards escaped.
    /// </summary>
    public static string Contains(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var escaped = text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

        return $"%{escaped}%";
    }
}
