namespace Sentium.Praxis.Contracts;

/// <summary>
/// Marks the current agent run as unattended: nobody can answer an approval request. An approval-gated tool runs only
/// when it is in <see cref="PreApprovedTools"/>; any other gated tool returns a denial to the agent instead of waiting.
/// Pre-approval replaces only the human step: Sentinel still evaluates every call.
/// </summary>
public sealed record UnattendedRunPolicy(IReadOnlySet<string> PreApprovedTools)
{
    public bool IsPreApproved(string toolName) => PreApprovedTools.Contains(toolName);
}
