namespace Sentium.Praxis.Contracts;

/// <summary>
/// The agent or saved workflow a Schedule runs, as the owner can currently see it.
/// </summary>
/// <param name="Name">The agent's canonical name or the workflow's name.</param>
/// <param name="GatedTools">The approval-gated tools the run could call, which are the ones the owner may pre-approve.</param>
public sealed record UnattendedRunTarget(string Name, IReadOnlyList<ToolDescriptor> GatedTools);
