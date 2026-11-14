using Sentium.Core.Results;

namespace Sentium.Mcp.Features.Servers;

public interface IMcpServerService
{
    Task<IReadOnlyList<McpServerDto>> GetAllAsync(CancellationToken ct);

    Task<McpServerDto?> GetAsync(Guid id, CancellationToken ct);

    Task<Result<McpServerDto>> CreateAsync(CreateMcpServerRequest request, CancellationToken ct);

    Task<Result<McpServerDto>> UpdateAsync(Guid id, UpdateMcpServerRequest request, CancellationToken ct);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Connects to the server and refreshes its tool list. New tools start disabled and unapproved; tools whose
    /// definition changed lose their approval until re-approved.
    /// </summary>
    Task<Result<McpServerDto>> SyncAsync(Guid id, CancellationToken ct);

    Task<Result<McpServerDto>> UpdateToolsAsync(Guid id, UpdateMcpToolsRequest request, CancellationToken ct);

    Task<McpServerStatusDto?> GetStatusAsync(Guid id, CancellationToken ct);
}
