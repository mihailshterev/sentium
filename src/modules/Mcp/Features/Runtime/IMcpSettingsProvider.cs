namespace Sentium.Mcp.Features.Runtime;

public interface IMcpSettingsProvider
{
    ValueTask<McpRuntimeSettings> GetAsync(CancellationToken ct = default);
}
