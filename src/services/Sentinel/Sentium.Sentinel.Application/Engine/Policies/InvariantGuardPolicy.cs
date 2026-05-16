using Microsoft.Extensions.Options;
using Sentium.Sentinel.Application.Options;
using Sentium.Sentinel.Core.Policies;

namespace Sentium.Sentinel.Application.Engine.Policies;

/// <summary>
/// Hard-constraint policy layer — the first gate in the PDP chain.
/// Denies requests that violate invariants that must hold regardless of any
/// other context: forbidden actions, protected resources, and wildcard resource access.
/// </summary>
public sealed class InvariantGuardPolicy(IOptions<PdpOptions> opts) : IPdpPolicy
{
    private readonly PdpOptions _options = opts.Value;

    public string Name => "InvariantGuard";

    public Task<PolicyDecision?> EvaluateAsync(PolicyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_options.LockdownMode &&
            !request.Action.Equals("read", StringComparison.OrdinalIgnoreCase) &&
            !request.Action.Equals("search", StringComparison.OrdinalIgnoreCase) &&
            !request.Action.Equals("list", StringComparison.OrdinalIgnoreCase) &&
            !request.Action.Equals("get", StringComparison.OrdinalIgnoreCase))
        {
            return Deny("Lockdown mode is active. Only Read/Search/List/Get actions are permitted.", PolicyRiskLevel.High, alert: false);
        }

        if (request.ResourceId is "*" or "**" or "all" or ".")
        {
            return Deny("Wildcard resource access is not permitted. Agents must specify explicit resource identifiers.", PolicyRiskLevel.Critical, alert: true);
        }

        var action = request.Action.AsSpan().Trim();
        foreach (var forbidden in _options.ForbiddenActions)
        {
            if (action.StartsWith(forbidden.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                return Deny($"Action '{request.Action}' is unconditionally forbidden.", PolicyRiskLevel.Critical, alert: true);
            }
        }

        var resourceId = request.ResourceId;
        foreach (var prefix in _options.ProtectedResourcePrefixes)
        {
            if (resourceId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return Deny($"Resource '{request.ResourceId}' is in a protected namespace and cannot be accessed by agents.", PolicyRiskLevel.Critical, alert: true);
            }
        }

        if (resourceId.Contains("sentinel", StringComparison.OrdinalIgnoreCase) && request.Action.Equals("write", StringComparison.OrdinalIgnoreCase))
        {
            return Deny("Agents are not permitted to write to Sentinel resources.", PolicyRiskLevel.Critical, alert: true);
        }

        return Task.FromResult<PolicyDecision?>(null);
    }

    private static Task<PolicyDecision?> Deny(string reason, PolicyRiskLevel risk, bool alert = false)
    {
        var decision = PolicyDecision.Deny(
            reason,
            Guid.Empty,
            [nameof(InvariantGuardPolicy)],
            risk,
            alert);

        return Task.FromResult<PolicyDecision?>(decision);
    }
}
