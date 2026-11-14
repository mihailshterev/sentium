using Sentium.Praxis.Features.Workflows;


namespace Sentium.Praxis.Entities;

internal sealed class WorkflowRun
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? WorkflowId { get; set; }
    public Workflow? Workflow { get; set; }
    public string TriggerType { get; set; } = null!;

    /// <summary>
    /// Short description of what ran - generated from the scenario, or set by the user. <see langword="null"/> for runs
    /// recorded before titles existed.
    /// </summary>
    public string? Title { get; set; }

    public string TriggerPayload { get; set; } = null!;
    public string Explanation { get; set; } = null!;
    public string Risk { get; set; } = null!;
    public string Recommendation { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public List<WorkflowLogEntry> Logs { get; set; } = [];
}
