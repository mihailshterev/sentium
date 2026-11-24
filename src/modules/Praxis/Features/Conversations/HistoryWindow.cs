namespace Sentium.Praxis.Features.Conversations;

/// <summary>
/// Which stored messages the model sees verbatim. The newest <see cref="RecentMessages"/> always; older ones only while
/// the running history summary does not cover them yet, so a message is never in neither. Everything the summary
/// covers is left out.
/// </summary>
internal static class HistoryWindow
{
    public const int RecentMessages = 20;

    /// <summary>
    /// How many messages beyond the recent ones wait before the summary takes them in.
    /// </summary>
    public const int SummaryBatchSize = 10;

    /// <summary>
    /// Enough to hold the recent messages, a full batch the summary has not taken in yet, and the turn in progress.
    /// </summary>
    public const int MaxLoadedMessages = RecentMessages + SummaryBatchSize + 2;

    /// <param name="newestLast">Up to <see cref="MaxLoadedMessages"/> of the newest messages, oldest first.</param>
    /// <param name="summarizedThrough">The newest message the summary covers; <see langword="null"/> without a summary.</param>
    public static IReadOnlyList<MessageResponse> Select(IReadOnlyList<MessageResponse> newestLast, DateTime? summarizedThrough)
    {
        ArgumentNullException.ThrowIfNull(newestLast);

        var keepFrom = Math.Max(0, newestLast.Count - RecentMessages);
        var through = summarizedThrough ?? DateTime.MinValue;

        while (keepFrom > 0 && newestLast[keepFrom - 1].Timestamp > through)
        {
            keepFrom--;
        }

        return keepFrom == 0 ? newestLast : [.. newestLast.Skip(keepFrom)];
    }
}
