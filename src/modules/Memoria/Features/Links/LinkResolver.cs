using Sentium.Memoria.Common;

namespace Sentium.Memoria.Features.Links;

/// <summary>
/// Resolves link keys to notes the way Obsidian does: a key matches a note whose path (without <c>.md</c>) equals it
/// or ends with <c>/key</c>, an exact path beating a partial one and otherwise the shortest path winning; failing
/// that, a key without <c>/</c> matches a frontmatter alias. Ties break by ordinal path so results are stable.
/// </summary>
internal sealed class LinkResolver
{
    private readonly Dictionary<string, List<NoteKey>> _byLinkKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<NoteKey>> _byAlias = new(StringComparer.Ordinal);

    public LinkResolver(IEnumerable<NoteKey> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);

        foreach (var note in notes)
        {
            foreach (var key in VaultPaths.LinkKeysFor(note.PathKey))
            {
                Add(_byLinkKey, key, note);
            }

            foreach (var alias in note.AliasKeys)
            {
                Add(_byAlias, alias, note);
            }
        }
    }

    public Guid? Resolve(string targetKey)
    {
        ArgumentNullException.ThrowIfNull(targetKey);

        if (_byLinkKey.TryGetValue(targetKey, out var candidates))
        {
            return candidates.FirstOrDefault(c => c.PathKey == targetKey)?.Id ?? Best(candidates);
        }

        if (!targetKey.Contains('/', StringComparison.Ordinal) && _byAlias.TryGetValue(targetKey, out var aliased))
        {
            return Best(aliased);
        }

        return null;
    }

    /// <summary>
    /// Every key whose resolution can change when a note at <paramref name="pathKey"/> with
    /// <paramref name="aliasKeys"/> appears or disappears.
    /// </summary>
    public static IEnumerable<string> KeysAffectedBy(string pathKey, IEnumerable<string> aliasKeys)
    {
        ArgumentNullException.ThrowIfNull(pathKey);
        ArgumentNullException.ThrowIfNull(aliasKeys);

        return VaultPaths.LinkKeysFor(pathKey).Concat(aliasKeys);
    }

    private static Guid Best(List<NoteKey> candidates)
    {
        return candidates
            .OrderBy(c => c.PathKey.Length)
            .ThenBy(c => c.PathKey, StringComparer.Ordinal)
            .First()
            .Id;
    }

    private static void Add(Dictionary<string, List<NoteKey>> index, string key, NoteKey note)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = [];
            index[key] = list;
        }

        list.Add(note);
    }
}
