using Sentium.Nuntius.Contracts;
using Sentium.Praxis.Features.Workflows;

namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// Tells a run's user how it ended. Every run notifies, watched or not: the portal cannot tell yet whether the user
/// is looking at the run, so this starts as "always notify" (refine only if it proves noisy).
/// </summary>
internal sealed class WorkflowRunNotifier(INotifier notifier)
{
    private const string OrchestrationLink = "/orchestration";
    private const int RequestPreviewMax = 80;

    public Task NotifyAsync(WorkflowTrigger trigger, PersistedRun? run, WorkflowRunOutcome outcome, string? reason, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        if (trigger.UserId is not { } userId || userId == Guid.Empty)
        {
            return Task.CompletedTask;
        }

        var name = run?.Title ?? Preview(trigger.ResolveActivity());
        var message = new NotificationMessage(
            NotificationCategories.WorkflowRun,
            outcome == WorkflowRunOutcome.Completed ? NotificationSeverity.Info : NotificationSeverity.Warning,
            $"“{name}” {Describe(outcome)}",
            reason,
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

    private static string Preview(string request)
    {
        var text = request.ReplaceLineEndings(" ").Trim();
        if (text.Length == 0)
        {
            return "Workflow run";
        }

        return text.Length <= RequestPreviewMax ? text : $"{text[..RequestPreviewMax].TrimEnd()}…";
    }
}
