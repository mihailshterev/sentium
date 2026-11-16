using Sentium.Registry.Contracts.Settings;

namespace Sentium.Praxis.Features.Registry;

internal sealed record InferenceSettingsSnapshot(
    string DefaultModel,
    float AgentTemperature)
{
    public static readonly InferenceSettingsSnapshot Default = From(new InferenceSettings());

    public static InferenceSettingsSnapshot From(InferenceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new(settings.DefaultModel, settings.AgentTemperature);
    }
}
