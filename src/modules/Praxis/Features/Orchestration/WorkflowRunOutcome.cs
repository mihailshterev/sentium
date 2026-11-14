namespace Sentium.Praxis.Features.Orchestration;

/// <summary>
/// How a workflow run ended, for the notification sent to its user. A run the user stopped themselves has no outcome
/// here: they already know.
/// </summary>
internal enum WorkflowRunOutcome
{
    Completed,
    Failed,
    TimedOut,
    Interrupted
}
