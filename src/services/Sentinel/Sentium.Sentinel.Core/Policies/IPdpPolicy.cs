namespace Sentium.Sentinel.Core.Policies;

/// <summary>
/// A single layer in the Defense-in-Depth PDP stack.
/// Policies are evaluated in registration order; the first deny short-circuits the chain.
/// </summary>
public interface IPdpPolicy
{
    /// <summary>
    /// Human-readable policy name used in audit records and telemetry.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Evaluates whether the <paramref name="request"/> should be permitted.
    /// </summary>
    /// <returns>
    /// A <see cref="PolicyDecision"/> when this policy produces a verdict,
    /// or <see langword="null"/> when the policy is not applicable and the engine
    /// should continue to the next layer.
    /// </returns>
    Task<PolicyDecision?> EvaluateAsync(PolicyRequest request, CancellationToken ct);
}
