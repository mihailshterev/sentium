namespace Sentium.Sentinel.Core.Dtos;

public sealed record PolicyEvaluationRequest
{
    public required string AgentId { get; init; }
    public required string SkillName { get; init; }
    public required string ResourceType { get; init; }
    public required string Action { get; init; }
    public required string OriginalUserPrompt { get; init; }
    public required string CorrelationId { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}
