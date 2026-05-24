using Sentium.AgentRuntime.Core.Agents;

namespace Sentium.AgentRuntime.Application.Agents.Native;

/// <summary>
/// A specialized native agent responsible for task decomposition and agent orchestration.
/// The PlannerAgent analyzes incoming requests to select the most appropriate expert agents
/// for the given problem domain.
/// </summary>
public sealed class PlannerAgent : IAgent
{
    /// <inheritdoc />
    /// <value>"Planner Agent"</value>
    public string Name => "Planner Agent";

    /// <inheritdoc />
    /// <remarks>
    /// This agent is strictly constrained to output valid JSON arrays. It serves as the
    /// "brain" or router of the system, determining the workflow based on the capabilities
    /// of the available Native agent roles.
    /// </remarks>
    public string Instructions => @"You are an orchestration agent. Analyze the input and determine which specialized agents are required to resolve the issue.

    Available Agents:
    - Forensics: Analyzes technical artifacts, Base64 strings, and binary obfuscation.
    - SecurityAnalyst: Analyzes network traffic and connection anomalies.
    - ThreatIntel: Evaluates Source IPs and matches behaviors against known APT groups.

    You MUST output strictly a JSON array of strings representing the required agent roles. Do not include markdown, explanations, or any other text.
    Example output: [""Forensics"", ""ThreatIntel""]";
}
