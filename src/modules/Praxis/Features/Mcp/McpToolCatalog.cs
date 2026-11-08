using System.Collections.Frozen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Data;
using Sentium.Praxis.Dtos;
using Sentium.Praxis.Entities;
using Sentium.Praxis.Features.Tools;

namespace Sentium.Praxis.Features.Mcp;

/// <summary>
/// The approved MCP tools, held in memory so tool listing, assignment validation and agent builds stay synchronous and
/// never touch a network. Rebuilt from the database after every MCP server change and on startup.
/// </summary>
public interface IMcpToolCatalog
{
    /// <summary>
    /// The usable MCP tools (server enabled, tool enabled, approved and unchanged, still listed by the server).
    /// </summary>
    IReadOnlyList<ToolDescriptor> Descriptors { get; }

    /// <summary>
    /// Whether <paramref name="name"/> is a known MCP tool name - including currently unusable ones, so agents that
    /// still list a disabled or changed tool keep validating.
    /// </summary>
    bool Contains(string name);

    /// <summary>
    /// The usable functions among <paramref name="names"/> (approval-gated tools wrapped in
    /// <see cref="ApprovalRequiredAIFunction"/>).
    /// </summary>
    IReadOnlyList<AIFunction> GetFunctions(IReadOnlySet<string> names);

    Task EnsureLoadedAsync(CancellationToken ct);

    Task RefreshAsync(CancellationToken ct);
}

public sealed class McpToolCatalog(
    IServiceScopeFactory scopeFactory,
    McpClientManager clients,
    IMcpSettingsProvider settings,
    ILogger<McpToolCatalog> logger) : IMcpToolCatalog, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile Snapshot snapshot = Snapshot.Empty;
    private volatile bool loaded;

    public IReadOnlyList<ToolDescriptor> Descriptors => snapshot.Descriptors;

    public bool Contains(string name) => name is not null && snapshot.KnownNames.Contains(name);

    public IReadOnlyList<AIFunction> GetFunctions(IReadOnlySet<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        var current = snapshot;
        return [.. names.Select(n => current.Functions.GetValueOrDefault(n)).OfType<AIFunction>()];
    }

    public async Task EnsureLoadedAsync(CancellationToken ct)
    {
        if (!loaded)
        {
            await RefreshAsync(ct);
        }
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PraxisDbContext>();

            var servers = await db.McpServers.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);

            snapshot = Build(servers);
            loaded = true;

            logger.LogDebug("MCP tool catalog loaded: {Usable} usable of {Known} known tools", snapshot.Descriptors.Count, snapshot.KnownNames.Count);
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();

    /// <summary>
    /// Whether an agent may be offered this tool.
    /// </summary>
    public static bool IsUsable(McpServer server, McpToolEntry tool)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(tool);

        return server.Enabled && tool.Enabled && !tool.Missing && tool.ApprovedHash is not null && tool.ApprovedHash == tool.DefinitionHash;
    }

    /// <summary>
    /// The risk shown in the tool picker, from the server's (untrusted, but approved) annotations.
    /// </summary>
    public static ToolRiskLevel Risk(McpToolEntry tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        return tool.DestructiveHint == true ? ToolRiskLevel.High
            : tool.ReadOnlyHint == true ? ToolRiskLevel.Low
            : ToolRiskLevel.Medium;
    }

    private Snapshot Build(IReadOnlyList<McpServer> servers)
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var descriptors = new List<ToolDescriptor>();
        var functions = new Dictionary<string, AIFunction>(StringComparer.OrdinalIgnoreCase);

        foreach (var server in servers)
        {
            var connection = McpServerConnectionInfo.From(server);

            foreach (var tool in server.Tools.OrderBy(t => t.QualifiedName, StringComparer.Ordinal))
            {
                known.Add(tool.QualifiedName);

                if (!IsUsable(server, tool))
                {
                    continue;
                }

                AIFunction function = new McpToolFunction(connection, tool.QualifiedName, tool.Name, tool.Description, tool.InputSchema, clients, settings);
                if (tool.RequiresApproval)
                {
                    function = new ApprovalRequiredAIFunction(function);
                }

                functions[tool.QualifiedName] = function;
                descriptors.Add(new ToolDescriptor(
                    tool.QualifiedName,
                    tool.Description ?? tool.Title ?? tool.Name,
                    nameof(ToolResource.External),
                    nameof(ToolAction.Execute),
                    Risk(tool).ToString(),
                    tool.RequiresApproval,
                    server.DisplayName));
            }
        }

        return new Snapshot(descriptors, known.ToFrozenSet(StringComparer.OrdinalIgnoreCase), functions.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase));
    }

    private sealed record Snapshot(
        IReadOnlyList<ToolDescriptor> Descriptors,
        FrozenSet<string> KnownNames,
        FrozenDictionary<string, AIFunction> Functions)
    {
        public static Snapshot Empty { get; } = new([], FrozenSet<string>.Empty, FrozenDictionary<string, AIFunction>.Empty);
    }
}
