using Microsoft.EntityFrameworkCore;
using Sentium.Praxis.Data;
using Sentium.Praxis.Entities;

namespace Sentium.Praxis.Features.Mcp;

public interface IMcpServerRepository
{
    Task<IReadOnlyList<McpServer>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// The server, tracked for update.
    /// </summary>
    Task<McpServer?> GetTrackedAsync(Guid id, CancellationToken ct);

    Task<bool> NameExistsAsync(string name, CancellationToken ct);

    Task AddAsync(McpServer server, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>
    /// Deletes the server and removes its tool names from every agent's tool list and built-in override, in one
    /// SaveChanges (atomic). Returns the owners of the agents that changed (for cache eviction).
    /// </summary>
    Task<IReadOnlyCollection<Guid>> DeleteAsync(McpServer server, CancellationToken ct);
}

public sealed class McpServerRepository(PraxisDbContext db) : IMcpServerRepository
{
    public async Task<IReadOnlyList<McpServer>> GetAllAsync(CancellationToken ct) =>
        await db.McpServers.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);

    public Task<McpServer?> GetTrackedAsync(Guid id, CancellationToken ct) =>
        db.McpServers.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<bool> NameExistsAsync(string name, CancellationToken ct) =>
        db.McpServers.AnyAsync(s => s.Name == name, ct);

    public async Task AddAsync(McpServer server, CancellationToken ct)
    {
        db.McpServers.Add(server);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<IReadOnlyCollection<Guid>> DeleteAsync(McpServer server, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(server);

        var prefix = server.Name + McpNames.Separator;
        bool IsServerTool(string tool) => tool.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);

        var agents = await db.Agents.IgnoreQueryFilters().Where(a => a.Tools != null).ToListAsync(ct);
        var overrides = await db.AgentToolOverrides.IgnoreQueryFilters().Where(o => o.Tools != null).ToListAsync(ct);

        var owners = new HashSet<Guid>();
        foreach (var agent in agents.Where(a => a.Tools!.Any(IsServerTool)))
        {
            agent.Tools = [.. agent.Tools!.Where(t => !IsServerTool(t))];
            owners.Add(agent.UserId);
        }

        foreach (var toolOverride in overrides.Where(o => o.Tools!.Any(IsServerTool)))
        {
            toolOverride.Tools = [.. toolOverride.Tools!.Where(t => !IsServerTool(t))];
            owners.Add(toolOverride.UserId);
        }

        db.McpServers.Remove(server);
        await db.SaveChangesAsync(ct);

        return owners;
    }
}
