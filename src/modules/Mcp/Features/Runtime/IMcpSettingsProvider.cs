namespace Sentium.Mcp.Features.Runtime;

internal interface IMcpSettingsProvider
{
    ValueTask<McpRuntimeSettings> GetAsync(CancellationToken ct = default);
}
