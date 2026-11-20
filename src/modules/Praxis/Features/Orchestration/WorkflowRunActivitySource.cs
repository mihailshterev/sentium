using Sentium.Infrastructure.BackgroundWork;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// Workflow runs in flight, plus the runs still waiting in the queue as one entry.
/// </summary>
internal sealed class WorkflowRunActivitySource(IWorkflowExecutionRegistry registry, WorkflowRunQueue queue) : IBackgroundActivitySource
{
    private const string Source = "Workflow runs";

    public Task<IReadOnlyList<BackgroundActivity>> GetActivitiesAsync(CancellationToken ct)
    {
        var activities = registry.Snapshot()
            .OrderBy(run => run.StartedAt)
            .Select(run => new BackgroundActivity(Source, run.Label ?? "Workflow run", $"Stream {run.StreamId}", run.OwnerId, run.StartedAt))
            .ToList();

        if (queue.Reader.CanCount && queue.Reader.Count > 0)
        {
            activities.Add(new BackgroundActivity(Source, "Queued runs", $"{queue.Reader.Count} waiting for a free slot", null, null));
        }

        return Task.FromResult<IReadOnlyList<BackgroundActivity>>(activities);
    }
}
