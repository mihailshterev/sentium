using Sentium.Sentinel.Features.RateLimiting;
using Sentium.Sentinel.Features.Settings;

namespace Sentium.Sentinel.Features.Policies;

/// <summary>
/// Sliding-window rate-limiting policy layer.
/// Prevents a single agent from overwhelming the platform or the PDP itself,
/// and limits the blast-radius of a compromised or prompt-injected agent. The window is per user and agent, so one
/// user's busy agent never throttles another user's agent of the same name.
/// </summary>
internal sealed class RateLimitingPolicy(IRateLimitStore store, IPdpRuntimeSettingsProvider settings) : IPdpPolicy
{
    public string Name => "RateLimiting";

    public async Task<PolicyDecision?> EvaluateAsync(PolicyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runtime = await settings.GetAsync(ct);

        var window = TimeSpan.FromSeconds(runtime.RateLimitWindowSeconds);
        var key = request.UserId is { } userId ? $"{userId:N}:{request.AgentId}" : request.AgentId;
        var allowed = store.TryConsume(key, window, runtime.RateLimitMaxRequests);

        if (!allowed)
        {
            var current = store.GetCurrentCount(key, window);
            return PolicyDecision.Deny(
                $"Agent '{request.AgentId}' has exceeded the rate limit " +
                $"({current}/{runtime.RateLimitMaxRequests} requests in the last {runtime.RateLimitWindowSeconds}s). " +
                "Please retry after the window resets.",
                Guid.Empty,
                [nameof(RateLimitingPolicy)],
                PolicyRiskLevel.High,
                alert: false);
        }

        return null;
    }
}
