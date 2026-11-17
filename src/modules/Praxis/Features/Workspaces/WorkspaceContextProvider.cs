using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Workspaces;

/// <summary>
/// Agent Framework context provider that puts the conversation's workspace and the files attached to the current
/// message into the agent's instructions (see <see cref="IWorkspaceContextBuilder"/>), so the agent sees them without
/// having to decide to call a tool first.
/// </summary>
/// <remarks>
/// <para>What to inject lives in the <see cref="AgentSession"/> (set by <see cref="Attach"/>), not in this instance: the
/// session is serialized when a run pauses for tool approval, so the resumed run - built by a new request with a new
/// agent - still sees the same files.</para>
/// <para>Contributes <see cref="AIContext.Instructions"/> (merged into the single system message) rather than an extra
/// user message, so small local models are not handed back-to-back user turns. A session without attachments gets no
/// context, which keeps squad and other agents that never attach anything unaffected.</para>
/// </remarks>
internal sealed class WorkspaceContextProvider(IWorkspaceContextBuilder builder, IPdpContextAccessor runContext, int contentBudgetChars)
    : AIContextProvider(null, null, null)
{
    // AIJsonUtilities options match how the framework serializes the rest of the session.
    private static readonly ProviderSessionState<State> SessionState =
        new(_ => new State(), nameof(WorkspaceContextProvider), AIJsonUtilities.DefaultOptions);

    /// <summary>
    /// Records on <paramref name="session"/> which workspace and attached files its runs should see.
    /// </summary>
    public static void Attach(AgentSession session, Guid? workspaceId, IReadOnlyList<Guid> fileIds)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(fileIds);

        if (workspaceId is null && fileIds.Count == 0)
        {
            return;
        }

        SessionState.SaveState(session, new State { WorkspaceId = workspaceId, FileIds = [.. fileIds] });
    }

    protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var state = SessionState.GetOrInitializeState(context.Session);
        if (runContext.UserId is not { } userId || (state.WorkspaceId is null && state.FileIds.Count == 0))
        {
            return new AIContext();
        }

        var block = await builder.BuildAsync(userId, state.WorkspaceId, state.FileIds, contentBudgetChars, cancellationToken);
        return block is null ? new AIContext() : new AIContext { Instructions = block };
    }

    private sealed class State
    {
        public Guid? WorkspaceId { get; set; }

        public List<Guid> FileIds { get; set; } = [];
    }
}
