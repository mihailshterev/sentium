using ModelContextProtocol.Protocol;
using Sentium.Mcp.Entities;

namespace Sentium.Mcp.Features.Servers;

public static class McpToolMerger
{
    /// <summary>
    /// Folds a fresh tool listing into the server's entries. Qualified names, approval and enablement survive a sync; a
    /// changed definition keeps its (now stale) approved hash, which is what marks it "changed".
    /// </summary>
    public static void Merge(McpServer server, IReadOnlyList<Tool> discovered)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(discovered);

        var existing = server.Tools.ToDictionary(t => t.Name, StringComparer.Ordinal);
        var taken = new HashSet<string>(server.Tools.Select(t => t.QualifiedName), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tool in discovered.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            if (!seen.Add(tool.Name))
            {
                continue;
            }

            var definition = McpDefinitionHasher.Describe(tool);

            if (!existing.TryGetValue(tool.Name, out var entry))
            {
                var qualifiedName = McpNames.Qualify(server.Name, tool.Name, taken);
                taken.Add(qualifiedName);

                entry = new McpToolEntry
                {
                    Name = tool.Name,
                    QualifiedName = qualifiedName,
                    DefinitionHash = definition.Hash,
                    Enabled = false,
                    RequiresApproval = true,
                };
                server.Tools.Add(entry);
            }

            entry.Title = definition.Title;
            entry.Description = definition.Description;
            entry.InputSchema = definition.InputSchema;
            entry.ReadOnlyHint = definition.ReadOnlyHint;
            entry.DestructiveHint = definition.DestructiveHint;
            entry.IdempotentHint = definition.IdempotentHint;
            entry.OpenWorldHint = definition.OpenWorldHint;
            entry.DefinitionHash = definition.Hash;
            entry.Missing = false;
        }

        foreach (var entry in server.Tools.Where(t => !seen.Contains(t.Name)))
        {
            entry.Missing = true;
        }
    }
}
