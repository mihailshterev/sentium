namespace Sentium.Locus.Features.Locations;

public sealed record LocationNode(Guid Id, Guid? ParentId, string Name);

public sealed record LocationCrumb(Guid Id, string Name);

/// <summary>
/// In-memory view of the whole location hierarchy. A household has at most a few hundred locations, so
/// loading them all is cheaper and far simpler than recursive SQL, and every traversal here is guarded
/// against cyclic data so corrupt rows can never loop forever.
/// </summary>
public sealed class LocationTree
{
    public const string PathSeparator = " › ";

    private static readonly string[] PathSeparators = ["/", ">", "›", "\\"];

    private readonly Dictionary<Guid, LocationNode> _nodes;
    private readonly Dictionary<Guid, List<LocationNode>> _children = [];
    private readonly List<LocationNode> _roots = [];

    public LocationTree(IEnumerable<LocationNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        _nodes = nodes.ToDictionary(n => n.Id);

        foreach (var node in _nodes.Values.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (node.ParentId is { } parentId && _nodes.ContainsKey(parentId))
            {
                if (!_children.TryGetValue(parentId, out var list))
                {
                    _children[parentId] = list = [];
                }

                list.Add(node);
            }
            else
            {
                _roots.Add(node);
            }
        }
    }

    public int Count => _nodes.Count;

    public IReadOnlyCollection<LocationNode> All => _nodes.Values;

    public IReadOnlyList<LocationNode> Roots => _roots;

    public bool Contains(Guid id) => _nodes.ContainsKey(id);

    public LocationNode? Find(Guid id) => _nodes.GetValueOrDefault(id);

    public IReadOnlyList<LocationNode> ChildrenOf(Guid id) => _children.TryGetValue(id, out var list) ? list : [];

    /// <summary>
    /// Root-first chain of ancestors ending with <paramref name="id"/>; empty when the id is unknown.
    /// </summary>
    public IReadOnlyList<LocationCrumb> GetPath(Guid id)
    {
        var path = new List<LocationCrumb>();
        var visited = new HashSet<Guid>();
        Guid? current = id;

        while (current is { } currentId && _nodes.TryGetValue(currentId, out var node) && visited.Add(currentId))
        {
            path.Add(new LocationCrumb(node.Id, node.Name));
            current = node.ParentId;
        }

        path.Reverse();
        return path;
    }

    /// <summary>
    /// Human-readable path such as "Home › Garage › Shelf B"; empty when the id is unknown.
    /// </summary>
    public string GetPathString(Guid id) => string.Join(PathSeparator, GetPath(id).Select(c => c.Name));

    /// <summary>
    /// 1 for a root, 2 for its children, ...; 0 when the id is unknown.
    /// </summary>
    public int GetDepth(Guid id) => GetPath(id).Count;

    /// <summary>
    /// The location and every location below it (breadth-first, each id once).
    /// </summary>
    public IReadOnlyList<Guid> GetSubtreeIds(Guid id)
    {
        if (!_nodes.ContainsKey(id))
        {
            return [];
        }

        var result = new List<Guid>();
        var visited = new HashSet<Guid>();
        var queue = new Queue<Guid>();
        queue.Enqueue(id);

        while (queue.TryDequeue(out var current))
        {
            if (!visited.Add(current))
            {
                continue;
            }

            result.Add(current);

            foreach (var child in ChildrenOf(current))
            {
                queue.Enqueue(child.Id);
            }
        }

        return result;
    }

    /// <summary>
    /// Number of levels below <paramref name="id"/> (0 for a leaf).
    /// </summary>
    public int GetHeight(Guid id)
    {
        var height = 0;
        var visited = new HashSet<Guid>();
        var frontier = new List<Guid> { id };

        while (true)
        {
            var next = new List<Guid>();
            foreach (var current in frontier)
            {
                if (!visited.Add(current))
                {
                    continue;
                }

                next.AddRange(ChildrenOf(current).Select(c => c.Id).Where(c => !visited.Contains(c)));
            }

            if (next.Count == 0)
            {
                return height;
            }

            height++;
            frontier = next;
        }
    }

    /// <summary>
    /// <see langword="true"/> when <paramref name="candidateId"/> is <paramref name="ancestorId"/> itself or
    /// sits anywhere below it - i.e. making <paramref name="candidateId"/> the parent of
    /// <paramref name="ancestorId"/> would create a cycle.
    /// </summary>
    public bool IsSelfOrDescendant(Guid candidateId, Guid ancestorId)
        => GetPath(candidateId).Any(c => c.Id == ancestorId);

    /// <summary>
    /// Checks whether a location (with its whole subtree) may be placed under <paramref name="newParentId"/>.
    /// Pass <see langword="null"/> for <paramref name="id"/> when validating a brand-new location.
    /// Returns an error message, or <see langword="null"/> when the placement is valid.
    /// </summary>
    public string? ValidatePlacement(Guid? id, Guid? newParentId, int maxDepth)
    {
        var height = id is { } existing ? GetHeight(existing) : 0;

        if (newParentId is not { } parentId)
        {
            return 1 + height > maxDepth ? DepthError(maxDepth) : null;
        }

        if (id is { } movingId && IsSelfOrDescendant(parentId, movingId))
        {
            return "A location cannot be moved inside itself or one of its sub-locations.";
        }

        return GetDepth(parentId) + 1 + height > maxDepth ? DepthError(maxDepth) : null;
    }

    /// <summary>
    /// Locations whose name contains <paramref name="term"/> (case-insensitive), with their whole subtrees.
    /// </summary>
    public IReadOnlySet<Guid> FindSubtreesMatching(string term)
    {
        ArgumentNullException.ThrowIfNull(term);

        var result = new HashSet<Guid>();
        foreach (var node in _nodes.Values.Where(n => n.Name.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            result.UnionWith(GetSubtreeIds(node.Id));
        }

        return result;
    }

    /// <summary>
    /// Resolves free-form text the way a person (or an assistant) refers to a place: an id, a path suffix
    /// such as "Garage/Shelf B" or "Home › Garage", an exact name, or - failing that - a partial name.
    /// </summary>
    public IReadOnlyList<LocationNode> Resolve(string reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var text = reference.Trim();

        if (text.Length == 0)
        {
            return [];
        }

        if (Guid.TryParse(text, out var id))
        {
            return Find(id) is { } byId ? [byId] : [];
        }

        var segments = text.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length > 1)
        {
            return OrderByPath(_nodes.Values.Where(n => PathEndsWith(n.Id, segments)));
        }

        var exact = _nodes.Values.Where(n => n.Name.Equals(text, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count > 0)
        {
            return OrderByPath(exact);
        }

        return OrderByPath(_nodes.Values.Where(n => n.Name.Contains(text, StringComparison.OrdinalIgnoreCase)));
    }

    private List<LocationNode> OrderByPath(IEnumerable<LocationNode> nodes)
        => [.. nodes.OrderBy(n => GetPathString(n.Id), StringComparer.OrdinalIgnoreCase)];

    private bool PathEndsWith(Guid id, string[] segments)
    {
        var path = GetPath(id);
        if (path.Count < segments.Length)
        {
            return false;
        }

        var offset = path.Count - segments.Length;
        for (var i = 0; i < segments.Length; i++)
        {
            if (!path[offset + i].Name.Equals(segments[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string DepthError(int maxDepth) => $"Locations can be nested at most {maxDepth} levels deep.";
}
