using System.Globalization;
using Quartz;
using Sentium.Praxis.Contracts;

namespace Sentium.Scheduling.Features.Jobs;

/// <summary>
/// The typed view of an agent or workflow Schedule's job data.
/// </summary>
internal sealed record ScheduleDefinition(
    ScheduleKind Kind,
    string Name,
    string? AgentName,
    Guid? WorkflowId,
    string TargetName,
    string Prompt,
    IReadOnlyList<string> PreApprovedTools)
{
    public static ScheduleKind ReadKind(JobDataMap data)
    {
        ArgumentNullException.ThrowIfNull(data);

        return Enum.TryParse<ScheduleKind>(data.Find(ScheduledJobData.Kind), out var kind) ? kind : ScheduleKind.Script;
    }

    /// <summary>
    /// <see langword="null"/> for a script Schedule.
    /// </summary>
    public static ScheduleDefinition? Read(JobDataMap data)
    {
        var kind = ReadKind(data);
        if (kind == ScheduleKind.Script)
        {
            return null;
        }

        var tools = data.Find(ScheduledJobData.PreApprovedTools) ?? string.Empty;

        return new ScheduleDefinition(
            kind,
            data.Find(ScheduledJobData.JobName) ?? string.Empty,
            kind == ScheduleKind.Agent ? data.Find(ScheduledJobData.AgentId) : null,
            Guid.TryParse(data.Find(ScheduledJobData.WorkflowId), out var workflowId) ? workflowId : null,
            data.Find(ScheduledJobData.TargetName) ?? string.Empty,
            data.Find(ScheduledJobData.Prompt) ?? string.Empty,
            tools.Split(ScheduledJobData.ToolSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    /// <summary>
    /// Writes every field, replacing what <paramref name="data"/> held for them; the last-run entries are left as they are.
    /// </summary>
    public void WriteTo(JobDataMap data)
    {
        ArgumentNullException.ThrowIfNull(data);

        data[ScheduledJobData.Kind] = Kind.ToString();
        data[ScheduledJobData.JobName] = Name;
        data[ScheduledJobData.AgentId] = AgentName ?? string.Empty;
        data[ScheduledJobData.WorkflowId] = WorkflowId?.ToString("D", CultureInfo.InvariantCulture) ?? string.Empty;
        data[ScheduledJobData.TargetName] = TargetName;
        data[ScheduledJobData.Prompt] = Prompt;
        data[ScheduledJobData.PreApprovedTools] = string.Join(ScheduledJobData.ToolSeparator, PreApprovedTools);
    }

    public UnattendedRunRequest ToRunRequest(string scheduleId, Guid ownerId)
    {
        return new UnattendedRunRequest(
            scheduleId,
            Name,
            ownerId,
            Kind == ScheduleKind.Workflow ? UnattendedRunKind.Workflow : UnattendedRunKind.Agent,
            AgentName,
            WorkflowId,
            Prompt,
            PreApprovedTools);
    }

    /// <summary>
    /// This edit with <paramref name="previous"/>'s Pre-approvals kept, or cleared when the edit changes what runs: the
    /// owner consented for one agent or workflow and one prompt, so they have to be asked again.
    /// </summary>
    public ScheduleDefinition CarryPreApprovalsFrom(ScheduleDefinition previous)
    {
        return this with { PreApprovedTools = ChangesWhatRunsComparedTo(previous) ? [] : previous.PreApprovedTools };
    }

    /// <summary>
    /// Whether moving from <paramref name="previous"/> changes what runs, which invalidates the owner's Pre-approvals.
    /// </summary>
    public bool ChangesWhatRunsComparedTo(ScheduleDefinition previous)
    {
        ArgumentNullException.ThrowIfNull(previous);

        return Kind != previous.Kind
            || !string.Equals(AgentName, previous.AgentName, StringComparison.OrdinalIgnoreCase)
            || WorkflowId != previous.WorkflowId
            || !string.Equals(Prompt, previous.Prompt, StringComparison.Ordinal);
    }
}
