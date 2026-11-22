using System.Threading.RateLimiting;

namespace Sentium.Sentinel.Features.RateLimiting;

/// <summary>
/// Sliding-window budgets per caller key, on the framework's <see cref="PartitionedRateLimiter"/>, which also drops the
/// windows of callers that went quiet. The limit and window are part of the partition, so a change to the Registry
/// settings applies to the next request.
/// </summary>
internal sealed class AgentRateLimiter : IDisposable
{
    private const int SegmentsPerWindow = 6;

    private readonly PartitionedRateLimiter<Budget> _limiter = PartitionedRateLimiter.Create<Budget, Budget>(
        budget => RateLimitPartition.GetSlidingWindowLimiter(budget, static b => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = b.MaxRequests,
            Window = b.Window,
            SegmentsPerWindow = SegmentsPerWindow,
            QueueLimit = 0,
            AutoReplenishment = true
        }));

    /// <summary>
    /// Counts one request against <paramref name="key"/>'s budget; <see langword="false"/> when it is spent.
    /// </summary>
    public bool TryAcquire(string key, TimeSpan window, int maxRequests)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        using var lease = _limiter.AttemptAcquire(new Budget(key.ToUpperInvariant(), window, maxRequests));
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();

    private readonly record struct Budget(string Key, TimeSpan Window, int MaxRequests);
}
