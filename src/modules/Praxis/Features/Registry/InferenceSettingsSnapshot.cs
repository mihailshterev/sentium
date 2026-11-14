using Sentium.Core.Constants;

namespace Sentium.Praxis.Features.Registry;

internal sealed record InferenceSettingsSnapshot(
    string DefaultModel,
    float AgentTemperature)
{
    /// <summary>
    /// Mirrors the defaults of Registry's <c>InferenceSettings</c>.
    /// </summary>
    public static readonly InferenceSettingsSnapshot Default = new(
        DefaultModel: AIModels.Gemma4_E4B,
        AgentTemperature: 0.3f);
}
