using Sentium.Praxis.Features.Workspaces;

namespace Sentium.Praxis.Features.Workflows;

/// <summary>
/// Adds the run's workspace and attached files (see <see cref="IWorkspaceContextBuilder"/>) to a workflow request.
/// </summary>
internal static class WorkspaceContextPrompt
{
    /// <summary>
    /// Smaller than a chat turn's budget: the request text reaches the planner and every step of the squad.
    /// </summary>
    internal const int MaxContentChars = 12_000;

    public static async Task<string> AugmentAsync(IWorkspaceContextBuilder builder, WorkflowTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(trigger);

        if (trigger.UserId is not { } userId || (trigger.WorkspaceId is null && trigger.FileIds.Count == 0))
        {
            return trigger.Activity;
        }

        var block = await builder.BuildAsync(userId, trigger.WorkspaceId, trigger.FileIds, MaxContentChars, ct);
        return block is null ? trigger.Activity : $"{trigger.Activity}\n\n{block}";
    }
}
