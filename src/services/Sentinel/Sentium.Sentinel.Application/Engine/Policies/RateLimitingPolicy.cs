using Microsoft.Extensions.Options;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Core.Policies;
using Sentium.Sentinel.Core.RateLimiting;

namespace Sentium.Sentinel.Application.Engine.Policies;

/// <summary>
/// Sliding-window rate-limiting policy layer.
/// Prevents a single agent from overwhelming the platform or the PDP itself,
/// and limits the blast-radius of a compromised or prompt-injected agent.
/// </summary>
public sealed class RateLimitingPolicy(IRateLimitStore store, IOptions<PdpOptions> opts) : IPdpPolicy
{
    private readonly PdpOptions _options = opts.Value;

    public string Name => "RateLimiting";

    public Task<PolicyDecision?> EvaluateAsync(PolicyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var window = TimeSpan.FromSeconds(_options.RateLimitWindowSeconds);
        var allowed = store.TryConsume(request.AgentId, window, _options.RateLimitMaxRequests);

        if (!allowed)
        {
            var current = store.GetCurrentCount(request.AgentId, window);
            var decision = PolicyDecision.Deny(
                $"Agent '{request.AgentId}' has exceeded the rate limit " +
                $"({current}/{_options.RateLimitMaxRequests} requests in the last {_options.RateLimitWindowSeconds}s). " +
                "Please retry after the window resets.",
                Guid.Empty,
                [nameof(RateLimitingPolicy)],
                PolicyRiskLevel.High,
                alert: false);

            return Task.FromResult<PolicyDecision?>(decision);
        }

        return Task.FromResult<PolicyDecision?>(null);
    }
}
