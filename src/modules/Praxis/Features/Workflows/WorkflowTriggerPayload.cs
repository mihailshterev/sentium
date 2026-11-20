using System.Text.Json.Serialization;

namespace Sentium.Praxis.Features.Workflows;

/// <summary>
/// The stored JSON shape of a <see cref="WorkflowTrigger"/>.
/// </summary>
internal sealed record WorkflowTriggerPayload(string Activity, Guid? WorkflowId, string? WorkflowName, Guid? WorkspaceId, IReadOnlyList<Guid>? FileIds = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AgentName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScheduleId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ScheduleName { get; init; }
}
