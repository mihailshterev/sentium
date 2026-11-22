using Sentium.Sentinel.Contracts;
using Sentium.Sentinel.Features.RateLimiting;
using Sentium.Sentinel.Features.Settings;

namespace Sentium.Sentinel.Features.Policies;

/// <summary>
/// Sliding-window rate-limiting policy layer.
/// Prevents a single agent from overwhelming the platform or the PDP itself,
/// and limits the blast-radius of a compromised or prompt-injected agent. The window is per user and agent, so one
/// user's busy agent never throttles another user's agent of the same name.
/// </summary>
internal sealed class RateLimitingPolicy(AgentRateLimiter limiter, IPdpRuntimeSettingsProvider settings) : IPdpPolicy
{
    public string Name => "RateLimiting";

    public async Task<PolicyDecision?> EvaluateAsync(PolicyEvaluationRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runtime = await settings.GetAsync(ct);

        var window = TimeSpan.FromSeconds(runtime.RateLimitWindowSeconds);
        var key = request.UserId is { } userId ? $"{userId:N}:{request.AgentId}" : request.AgentId;
        if (!limiter.TryAcquire(key, window, runtime.RateLimitMaxRequests))
        {
            return PolicyDecision.Deny(
                $"Agent '{request.AgentId}' has exceeded the rate limit " +
                $"({runtime.RateLimitMaxRequests} requests in {runtime.RateLimitWindowSeconds}s). " +
                "Please retry after the window resets.",
                Guid.Empty,
                [nameof(RateLimitingPolicy)],
                PolicyRiskLevel.High,
                alert: false);
        }

        return null;
    }
}
