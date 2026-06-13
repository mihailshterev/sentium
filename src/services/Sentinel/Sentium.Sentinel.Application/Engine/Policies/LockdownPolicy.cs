using Sentium.Sentinel.Core.Policies;
using Sentium.Sentinel.Core.Settings;

namespace Sentium.Sentinel.Application.Engine.Policies;

/// <summary>
/// Global kill-switch policy layer.
/// When the Sovereign enables <see cref="PdpRuntimeSettings.LockdownMode"/>, every user-facing
/// agent action is denied. This is the platform's incident-response containment control: a single
/// runtime flag that halts all autonomous activity.
/// <para/>
/// System / Sovereign traffic bypasses Sentinel entirely at the authentication layer
/// (<c>SystemCaller</c> policy), so a blanket deny here only affects untrusted, user-facing
/// agent actions - exactly the intent of a lockdown. Registered first so it short-circuits
/// the chain before any other (more expensive) policy runs.
/// </summary>
public sealed class LockdownPolicy(IPdpRuntimeSettingsProvider settings) : IPdpPolicy
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
