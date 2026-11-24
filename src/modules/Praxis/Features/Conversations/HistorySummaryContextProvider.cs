using Microsoft.Agents.AI;
using Sentium.Praxis.Contracts;

namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Gives the agent the running summary of what its conversation said before the messages it sees verbatim (see
/// <see cref="HistoryWindow"/>). Contributes <see cref="AIContext.Instructions"/>, merged into the one system message:
/// a second system message or an extra user turn breaks chat templates that require alternating roles.
/// </summary>
internal sealed class HistorySummaryContextProvider(IConversationRepository repository, IPdpContextAccessor runContext) : AIContextProvider(null, null, null)
{
    protected override async ValueTask<AIContext> ProvideAIContextAsync(InvokingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (ConversationChatHistoryProvider.ConversationIdOf(context.Session) is not { } conversationId
            || runContext.UserId is not { } userId
            || await repository.GetHistorySummaryAsync(conversationId, userId, cancellationToken) is not { } summary)
        {
            return new AIContext();
        }

        return new AIContext { Instructions = BuildBlock(summary.Text) };
    }

    internal static string BuildBlock(string summary)
    {
        return $"""
            ### EARLIER IN THIS CONVERSATION
            The messages before the ones you can see were summarized. Treat this as what the user and you already said:
            {summary.Trim()}
            """;
    }
}
