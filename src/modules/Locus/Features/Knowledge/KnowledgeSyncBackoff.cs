namespace Sentium.Locus.Features.Knowledge;

internal static class KnowledgeSyncBackoff
{
    /// <summary>
    /// After this many failed attempts an entry is marked failed and waits for an edit or a manual retry.
    /// </summary>
    public const int MaxAttempts = 8;

    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromHours(1);

    /// <summary>
    /// Delay before the next try after <paramref name="attempts"/> failures: 30s, 1m, 2m, ... capped at 1h.
    /// </summary>
    public static TimeSpan DelayAfter(int attempts)
    {
        if (attempts <= 0)
        {
            return TimeSpan.Zero;
        }

        var exponent = Math.Min(attempts - 1, 16);
        var delay = TimeSpan.FromTicks(BaseDelay.Ticks * (1L << exponent));
        return delay > MaxDelay ? MaxDelay : delay;
    }
}
