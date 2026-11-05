namespace Sentium.Sentinel.Contracts;

/// <summary>
/// The Sentinel Policy Decision Point. Every protected agent tool call and sandbox execution is
/// authorized through it before it runs.
/// </summary>
public interface IPolicyDecisionPoint
{
    /// <summary>
    /// Evaluates the request against the full policy chain and records a forensic audit entry.
    /// Never throws for a policy failure: an unknown resource type or an engine fault yields a deny.
    /// </summary>
    Task<PolicyEvaluationResponse> EvaluateAsync(PolicyEvaluationRequest request, CancellationToken ct = default);
}
