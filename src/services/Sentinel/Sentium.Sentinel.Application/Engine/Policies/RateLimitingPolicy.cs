using Sentium.Sentinel.Core.Policies;
using Sentium.Sentinel.Core.RateLimiting;
using Sentium.Sentinel.Core.Settings;

namespace Sentium.Sentinel.Application.Engine.Policies;

/// <summary>
/// Sliding-window rate-limiting policy layer.
/// Prevents a single agent from overwhelming the platform or the PDP itself,
/// and limits the blast-radius of a compromised or prompt-injected agent.
/// </summary>
public sealed class RateLimitingPolicy(IRateLimitStore store, IPdpRuntimeSettingsProvider settings) : IPdpPolicy
{
    public string Name => "RateLimiting";

    public async Task<PolicyDecision?> EvaluateAsync(PolicyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runtime = await settings.GetAsync(ct);

        var window = TimeSpan.FromSeconds(runtime.RateLimitWindowSeconds);
        var allowed = store.TryConsume(request.AgentId, window, runtime.RateLimitMaxRequests);

        if (!allowed)
        {
            var current = store.GetCurrentCount(request.AgentId, window);
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
