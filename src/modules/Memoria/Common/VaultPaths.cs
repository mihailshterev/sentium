namespace Sentium.Memoria.Common;

/// <summary>
/// Vault-relative note paths: <c>/</c>-separated, no leading slash, no <c>.</c> or <c>..</c> segments, ending in
/// <c>.md</c>. Keys are the lower-cased path without extension, the form Obsidian links resolve against.
/// </summary>
internal static class VaultPaths
{
    public const string NoteExtension = ".md";

    /// <summary>
    /// The canonical form of <paramref name="raw"/>, or <see langword="null"/> when it is not a safe relative
    /// markdown path.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var path = raw.Trim().Replace('\\', '/');
        if (path.StartsWith('/') || path.Contains(':', StringComparison.Ordinal) || path.Any(char.IsControl))
        {
            return null;
        }

        var segments = path.Split('/');
        if (segments.Any(s => s.Length == 0 || s == "." || s == ".." || s.Trim().Length == 0))
        {
            return null;
        }

        if (!path.EndsWith(NoteExtension, StringComparison.OrdinalIgnoreCase) || path.Length <= NoteExtension.Length)
        {
            return null;
        }

        return path.Length > MemoriaLimits.PathMax ? null : path;
    }

    public static string ToKey(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var withoutExtension = path.EndsWith(NoteExtension, StringComparison.OrdinalIgnoreCase)
            ? path[..^NoteExtension.Length]
            : path;

        return withoutExtension.ToLowerInvariant();
    }

    public static string TitleOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var name = path[(path.LastIndexOf('/') + 1)..];
        return name.EndsWith(NoteExtension, StringComparison.OrdinalIgnoreCase) ? name[..^NoteExtension.Length] : name;
    }

    /// <summary>
    /// The folder part of a path or key, empty at the vault root.
    /// </summary>
    public static string FolderOf(string pathOrKey)
    {
        ArgumentNullException.ThrowIfNull(pathOrKey);

        var slash = pathOrKey.LastIndexOf('/');
        return slash < 0 ? string.Empty : pathOrKey[..slash];
    }

    /// <summary>
    /// Every key a link could use to reach the note at <paramref name="pathKey"/>: the full key and each shorter
    /// trailing part (<c>a/b/c</c> gives <c>a/b/c</c>, <c>b/c</c> and <c>c</c>).
    /// </summary>
    public static IEnumerable<string> LinkKeysFor(string pathKey)
    {
        ArgumentNullException.ThrowIfNull(pathKey);

        var start = 0;
        while (true)
        {
            yield return pathKey[start..];

            var slash = pathKey.IndexOf('/', start);
            if (slash < 0)
            {
                yield break;
            }

            start = slash + 1;
        }
    }

    /// <summary>
    /// Resolves a <c>./</c> or <c>../</c> link relative to <paramref name="sourceFolder"/>; <see langword="null"/>
    /// when it climbs above the vault root.
    /// </summary>
    public static string? Combine(string sourceFolder, string relative)
    {
        ArgumentNullException.ThrowIfNull(sourceFolder);
        ArgumentNullException.ThrowIfNull(relative);

        var parts = sourceFolder.Length == 0 ? new List<string>() : [.. sourceFolder.Split('/')];

        foreach (var segment in relative.Split('/'))
        {
            switch (segment)
            {
                case "" or ".":
                    continue;
                case "..":
                    if (parts.Count == 0)
                    {
                        return null;
                    }

                    parts.RemoveAt(parts.Count - 1);
                    break;
                default:
                    parts.Add(segment);
                    break;
            }
        }

        return parts.Count == 0 ? null : string.Join('/', parts);
    }
}
