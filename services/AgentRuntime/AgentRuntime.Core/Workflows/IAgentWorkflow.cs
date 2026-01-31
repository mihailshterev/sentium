namespace AgentRuntime.Core.Workflows;

public interface IAgentWorkflow
{
    WorkflowType Type { get; }
    Task<IReadOnlyList<string>> ExecuteAsync(string input, CancellationToken ct);
}
