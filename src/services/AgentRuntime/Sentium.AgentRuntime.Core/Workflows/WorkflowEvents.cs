namespace Sentium.AgentRuntime.Core.Workflows;

public static class WorkflowEvents
{
    public const string CustomWorkflow = "workflow.custom.workflow";
    public const string Dynamic = "workflow.dynamic";
    public const string AllEvents = "workflow.>";
    public const string CancelSignal = "orchestration.cancel";
    public const string StreamName = "WORKFLOWS";
    public const string ConsumerName = "agent-runtime-workflows";
}

public sealed record WorkflowCancelRequest(string StreamId);
