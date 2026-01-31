namespace AgentRuntime.Core.Workflows;

public sealed class WorkflowResult
{
    public string Explanation { get; init; } = "";
    public object Risk { get; init; } = default!;
    public object? History { get; init; }
    public object Recommendation { get; init; } = default!;
}
