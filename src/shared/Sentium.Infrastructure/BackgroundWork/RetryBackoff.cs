namespace Sentium.Infrastructure.BackgroundWork;

/// <summary>
/// The retry schedule of the outboxes: 30s, 1m, 2m, ... capped at 1h, for at most <see cref="MaxAttempts"/> attempts.
/// </summary>
public static class RetryBackoff
{
    /// <summary>
    /// After this many failed attempts an entry is marked failed and waits for the next change to its source.
    /// </summary>
    public const int MaxAttempts = 8;

    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromHours(1);

    /// <summary>
    /// Delay before the next try after <paramref name="attempts"/> failures.
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
