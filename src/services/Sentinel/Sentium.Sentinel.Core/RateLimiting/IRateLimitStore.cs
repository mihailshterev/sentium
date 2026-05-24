namespace Sentium.Sentinel.Core.RateLimiting;

/// <summary>
/// Sliding-window rate limit store for per-agent throttling.
/// Thread-safe implementations required.
/// </summary>
public interface IRateLimitStore
{
    /// <summary>
    /// Records a new request attempt for the given <paramref name="agentId"/> and
    /// returns whether the agent is within its allowed quota.
    /// </summary>
    /// <param name="agentId">Agent identifier.</param>
    /// <param name="window">Length of the sliding window.</param>
    /// <param name="maxRequests">Maximum number of requests allowed within the window.</param>
    /// <returns><see langword="true"/> if the request is within quota; <see langword="false"/> if the limit has been reached.</returns>
    bool TryConsume(string agentId, TimeSpan window, int maxRequests);

    /// <summary>
    /// Returns the number of requests recorded for <paramref name="agentId"/> within the current window.
    /// </summary>
    /// <param name="agentId">Agent identifier.</param>
    /// <param name="window">Length of the sliding window.</param>
    /// <returns>The number of requests recorded for the agent within the current window.</returns>
    int GetCurrentCount(string agentId, TimeSpan window);
}
