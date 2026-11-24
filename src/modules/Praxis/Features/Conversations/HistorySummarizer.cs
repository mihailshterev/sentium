namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Brings a conversation's running summary up to date once at least <see cref="HistoryWindow.SummaryBatchSize"/>
/// messages have moved past the recent window, a pass at a time so a small model never sees more than
/// <see cref="MaxInputChars"/> of messages at once. A long conversation from before summaries existed catches up in
/// several passes.
/// </summary>
internal sealed class HistorySummarizer(IConversationRepository repository, HistorySummaryGenerator generator)
{
    private const int MaxInputChars = 12_000;
    private const int MaxMessagesPerPass = 40;

    /// <summary>
    /// Bounds one call's work; whatever is left is picked up after the next turn.
    /// </summary>
    private const int MaxPasses = 10;

    /// <returns>The number of passes that updated the summary.</returns>
    public async Task<int> SummarizeIfDueAsync(Guid conversationId, Guid userId, CancellationToken ct)
    {
        var passes = 0;
        while (passes < MaxPasses)
        {
            var current = await repository.GetHistorySummaryAsync(conversationId, userId, ct);
            var pending = await repository.GetMessagesBeyondWindowAsync(
                conversationId, userId, current?.Through, HistoryWindow.RecentMessages, MaxMessagesPerPass, ct);

            if (pending.Count < HistoryWindow.SummaryBatchSize)
            {
                break;
            }

            var pass = TakeWithinBudget(pending);
            var summary = await generator.SummarizeAsync(current?.Text, pass, ct);
            if (summary is null)
            {
                break;
            }

            await repository.SaveHistorySummaryAsync(conversationId, userId, new HistorySummary(summary, pass[^1].Timestamp), ct);
            passes++;
        }

        return passes;
    }

    private static List<MessageResponse> TakeWithinBudget(IReadOnlyList<MessageResponse> pending)
    {
        var pass = new List<MessageResponse>();
        var used = 0;

        foreach (var message in pending)
        {
            var cost = HistorySummaryGenerator.CostOf(message);
            if (pass.Count > 0 && used + cost > MaxInputChars)
            {
                break;
            }

            pass.Add(message);
            used += cost;
        }

        return pass;
    }
}
