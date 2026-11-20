namespace Sentium.Praxis.Contracts;

/// <summary>
/// One fire of a Schedule. <see cref="AgentName"/> is set for an <see cref="UnattendedRunKind.Agent"/> run and
/// <see cref="WorkflowId"/> for a <see cref="UnattendedRunKind.Workflow"/> run; <see cref="Prompt"/> is the agent prompt
/// or the workflow's input.
/// </summary>
public sealed record UnattendedRunRequest(
    string ScheduleId,
    string ScheduleName,
    Guid OwnerId,
    UnattendedRunKind Kind,
    string? AgentName,
    Guid? WorkflowId,
    string Prompt,
    IReadOnlyCollection<string> PreApprovedTools);
