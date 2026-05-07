using Sentium.Shared.Constants;

namespace Sentium.AgentRuntime.Infrastructure;

public sealed class OllamaOptions
{
    public const string SectionName = "AI";

    public Uri BaseUrl { get; init; } = new("http://localhost:11434");

    public string DefaultModel { get; init; } = AIModels.Gemma4;
}
