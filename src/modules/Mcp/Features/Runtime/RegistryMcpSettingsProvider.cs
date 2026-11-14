using Sentium.Core.Constants;
using Sentium.Registry.Contracts;

namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// Reads the MCP settings from the Registry module in-process (Registry caches and evicts the envelope itself).
/// </summary>
public sealed class RegistryMcpSettingsProvider(
    ISettingsService settingsService,
    ILogger<RegistryMcpSettingsProvider> logger) : IMcpSettingsProvider
{
    public async ValueTask<McpRuntimeSettings> GetAsync(CancellationToken ct = default)
    {
        try
        {
            return await settingsService.GetValueAsync<McpRuntimeSettings>(SettingsKeys.Mcp, ct: ct) ?? McpRuntimeSettings.Default;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to read MCP settings from Registry; using defaults");
            return McpRuntimeSettings.Default;
        }
    }
}
