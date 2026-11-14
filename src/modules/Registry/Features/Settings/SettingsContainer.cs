namespace Sentium.Registry.Features.Settings;

/// <summary>
/// Root JSON payload stored in the SystemSettings.Settings column.
/// </summary>
internal sealed class SettingsContainer
{
    /// <summary>
    /// Per-user agent harness settings.
    /// </summary>
    public HarnessSettings Harness { get; set; } = new();

    /// <summary>
    /// Global Policy Decision Point settings.
    /// </summary>
    public PdpSettings Pdp { get; set; } = new();

    /// <summary>
    /// Global agent inference settings (default model, temperature).
    /// </summary>
    public InferenceSettings Inference { get; set; } = new();

    /// <summary>
    /// Global Vigil monitoring settings.
    /// </summary>
    public VigilSettings Vigil { get; set; } = new();

    /// <summary>
    /// Global MCP (Model Context Protocol) client settings.
    /// </summary>
    public McpSettings Mcp { get; set; } = new();
}
