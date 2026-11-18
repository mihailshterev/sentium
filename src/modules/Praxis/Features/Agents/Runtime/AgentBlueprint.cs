using Microsoft.Extensions.AI;
using Sentium.Praxis.Features.Registry;

namespace Sentium.Praxis.Features.Agents.Runtime;

/// <summary>
/// Everything that decides how an agent behaves for one user, before it is turned into a runnable agent: its
/// definition, the user's settings, the model and that model's context window, and its tools.
/// </summary>
internal sealed record AgentBlueprint(IAgent Definition, SettingsSnapshot Settings, string Model, int ContextWindow, List<AITool> Tools)
{
    public float Temperature => Settings.Inference?.AgentTemperature ?? InferenceSettingsSnapshot.Default.AgentTemperature;

    public string ComposeInstructions(string? overrideInstructions = null)
        => AgentInstructions.Compose(Settings.Harness, overrideInstructions ?? Definition.Instructions, [.. Tools.OfType<AIFunction>()]);
}
