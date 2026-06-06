using Sentium.Shared.Constants;

namespace Sentium.Registry.Core.Settings;

public sealed class OllamaSettings
{
    public string DefaultModel { get; set; } = AIModels.Gemma4_E4B;

    public float AgentTemperature { get; set; } = 0.3f;

    public int AgentContextWindow { get; set; } = 16384;
}
