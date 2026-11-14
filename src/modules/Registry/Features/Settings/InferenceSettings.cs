using Sentium.Core.Constants;

namespace Sentium.Registry.Features.Settings;

/// <summary>
/// Global agent inference settings. Server endpoints and per-model context sizes are deployment
/// configuration (the <c>Inference</c> section and the server's model presets), not runtime settings.
/// </summary>
internal sealed class InferenceSettings
{
    public string DefaultModel { get; set; } = AIModels.Gemma4_E4B;

    public float AgentTemperature { get; set; } = 0.3f;
}
