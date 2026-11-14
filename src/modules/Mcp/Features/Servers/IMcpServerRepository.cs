using Sentium.Mcp.Entities;

namespace Sentium.Mcp.Features.Servers;

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

    Task DeleteAsync(McpServer server, CancellationToken ct);
}
