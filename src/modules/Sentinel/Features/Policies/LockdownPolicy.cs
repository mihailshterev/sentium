using Sentium.Sentinel.Features.Settings;

namespace Sentium.Sentinel.Features.Policies;

/// <summary>
/// Global kill-switch policy layer.
/// When the Sovereign enables <see cref="PdpRuntimeSettings.LockdownMode"/>, every user-facing
/// agent action is denied. This is the platform's incident-response containment control: a single
/// runtime flag that halts all autonomous activity.
/// <para/>
/// Only agent tool calls and sandbox runs are evaluated by the PDP; the Sovereign's own requests (lifting the
/// lockdown included) never reach it, so a blanket deny here halts agents without locking the Sovereign out.
/// Registered first so it short-circuits the chain before any other (more expensive) policy runs.
/// </summary>
internal sealed class LockdownPolicy(IPdpRuntimeSettingsProvider settings) : IPdpPolicy
{
    public string Name => "Lockdown";

    public async Task<PolicyDecision?> EvaluateAsync(PolicyRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runtime = await settings.GetAsync(ct);

        if (!runtime.LockdownMode)
        {
            return null;
        }

        return PolicyDecision.Deny(
            "System is in lockdown mode. All agent actions are temporarily suspended by the Sovereign. " +
            "Contact a sovereign to lift the lockdown.",
            Guid.Empty,
            [Name],
            PolicyRiskLevel.Critical,
            alert: true
        );
    }
}
