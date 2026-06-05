using System.Text.Json.Serialization;
using Sentium.Shared.Constants;

namespace Sentium.Registry.Core.Settings;

public sealed class OllamaSettings
{
    [JsonPropertyName("defaultModel")]
    public string DefaultModel { get; set; } = AIModels.Gemma4_E4B;

    [JsonPropertyName("agentTemperature")]
    public float AgentTemperature { get; set; } = 0.3f;

    [JsonPropertyName("agentContextWindow")]
    public int AgentContextWindow { get; set; } = 16384;
}
