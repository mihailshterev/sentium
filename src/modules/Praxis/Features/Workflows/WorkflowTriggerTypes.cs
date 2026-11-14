namespace Sentium.Praxis.Features.Workflows;

/// <summary>
/// Kinds of workflow run. The values are persisted as <c>WorkflowRun.TriggerType</c>, so keep them stable.
/// </summary>
internal static class WorkflowTriggerTypes
{
    public const string CustomWorkflow = "workflow.custom.workflow";
    public const string Dynamic = "workflow.dynamic";
}
