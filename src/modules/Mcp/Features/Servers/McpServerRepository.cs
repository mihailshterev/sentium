using Microsoft.EntityFrameworkCore;
using Sentium.Mcp.Data;
using Sentium.Mcp.Entities;

namespace Sentium.Mcp.Features.Servers;

internal sealed class McpServerRepository(McpDbContext db) : IMcpServerRepository
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

    public async Task DeleteAsync(McpServer server, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(server);

        db.McpServers.Remove(server);
        await db.SaveChangesAsync(ct);
    }
}
