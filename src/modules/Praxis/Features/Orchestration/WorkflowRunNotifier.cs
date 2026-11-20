using Sentium.Nuntius.Contracts;
using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// Tells a run's user how it ended. Every run notifies, watched or not: the portal cannot tell yet whether the user
/// is looking at the run, so this starts as "always notify" (refine only if it proves noisy). A Schedule's run is a
/// <see cref="NotificationCategories.ScheduleRun"/> carrying an excerpt of the final answer, since nobody watched it.
/// </summary>
internal sealed class WorkflowRunNotifier(INotifier notifier)
{
    private const string OrchestrationLink = "/orchestration";
    private const int RequestPreviewMax = 80;
    private const int AnswerExcerptMax = 280;

    /// <param name="detail">The failure reason, or for a completed run its final answer.</param>
    public Task NotifyAsync(WorkflowTrigger trigger, PersistedRun? run, WorkflowRunOutcome outcome, string? detail, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        if (trigger.UserId is not { } userId || userId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        var completed = outcome == WorkflowRunOutcome.Completed;
        var scheduled = trigger.Schedule is not null;
        var name = run?.Title ?? trigger.Schedule?.ScheduleName ?? Preview(trigger.Activity, RequestPreviewMax);

        var message = new NotificationMessage(
            scheduled ? NotificationCategories.ScheduleRun : NotificationCategories.WorkflowRun,
            completed ? NotificationSeverity.Info : NotificationSeverity.Warning,
            $"“{name}” {Describe(outcome)}",
            completed ? (scheduled ? Excerpt(detail) : null) : detail,
            run is null ? OrchestrationLink : $"{OrchestrationLink}/runs/{run.Id}");

        return notifier.NotifyAsync(NotificationRecipient.User(userId), message, ct);
    }

    private static string Describe(WorkflowRunOutcome outcome)
    {
        return outcome switch
        {
            WorkflowRunOutcome.Completed => "finished",
            WorkflowRunOutcome.TimedOut => "timed out",
            WorkflowRunOutcome.Interrupted => "was interrupted",
            _ => "failed"
        };
    }

    private static string? Excerpt(string? answer)
    {
        return string.IsNullOrWhiteSpace(answer) ? null : Preview(answer, AnswerExcerptMax);
    }

    private static string Preview(string text, int max)
    {
        var oneLine = text.ReplaceLineEndings(" ").Trim();
        if (oneLine.Length == 0)
        {
            return "Workflow run";
        }

        return oneLine.Length <= max ? oneLine : $"{oneLine[..max].TrimEnd()}…";
    }
}
