using Sentium.Registry.Contracts.Settings;

namespace Sentium.Mcp.Features.Runtime;

/// <summary>
/// Runtime MCP client settings (Registry key <c>mcp</c>).
/// </summary>
internal sealed record McpRuntimeSettings(
    bool Enabled = true,
    int ConnectTimeoutSeconds = 30,
    int CallTimeoutSeconds = 60,
    int IdleTimeoutMinutes = 15,
    int MaxResultChars = 16_000)
{
    public static McpRuntimeSettings Default { get; } = From(new McpSettings());

    public static McpRuntimeSettings From(McpSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new(settings.Enabled, settings.ConnectTimeoutSeconds, settings.CallTimeoutSeconds, settings.IdleTimeoutMinutes, settings.MaxResultChars);
    }
}
