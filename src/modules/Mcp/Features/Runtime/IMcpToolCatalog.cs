using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;

namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// The approved MCP tools, held in memory so tool listing, assignment validation and agent builds stay synchronous and
/// never touch a network. Rebuilt from the database after every MCP server change and on startup.
/// </summary>
public interface IMcpToolCatalog : IAgentToolSource
{
    /// <summary>
    /// The usable functions among <paramref name="names"/> (approval-gated tools wrapped in
    /// <see cref="ApprovalRequiredAIFunction"/>).
    /// </summary>
    IReadOnlyList<AIFunction> GetFunctions(IReadOnlySet<string> names);

    Task EnsureLoadedAsync(CancellationToken ct);

    Task RefreshAsync(CancellationToken ct);
}
