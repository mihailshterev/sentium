using System.Text.Json;

namespace Sentium.Praxis.Features.Workflows;

/// <summary>
/// A queued workflow run. It travels in-process, so it is typed; only <see cref="ToPayloadJson"/> turns it into the
/// JSON kept on the run's history row.
/// </summary>
internal sealed class WorkflowTrigger
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public string TriggerType { get; init; } = "";

    /// <summary>
    /// The user's request text.
    /// </summary>
    public string Activity { get; init; } = "";

    /// <summary>
    /// The saved workflow to run, for a custom workflow run.
    /// </summary>
    public Guid? WorkflowId { get; init; }

    public string? WorkflowName { get; init; }

    /// <summary>
    /// The one agent to run, for a Schedule's agent run (a custom workflow run without a saved workflow).
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// Set when a Schedule started the run; nobody is present to approve anything.
    /// </summary>
    public ScheduledRun? Schedule { get; init; }

    public Guid? WorkspaceId { get; init; }

    /// <summary>
    /// Workspace files attached to the request, whose text the run's agents get up front.
    /// </summary>
    public IReadOnlyList<Guid> FileIds { get; init; } = [];

    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public Guid? UserId { get; init; }

    public string StreamId
    {
        get => string.IsNullOrEmpty(field) ? TriggerType : field;
        init;
    }

    /// <summary>
    /// The trigger as stored in <c>WorkflowRun.TriggerPayload</c>; the portal's run history reads its <c>activity</c>
    /// and <c>workflowName</c>.
    /// </summary>
    public string ToPayloadJson()
    {
        var payload = new WorkflowTriggerPayload(Activity, WorkflowId, WorkflowName, WorkspaceId, FileIds.Count > 0 ? FileIds : null)
        {
            AgentName = AgentName,
            ScheduleId = Schedule?.ScheduleId,
            ScheduleName = Schedule?.ScheduleName,
        };

        return JsonSerializer.Serialize(payload, PayloadJsonOptions);
    }
}
