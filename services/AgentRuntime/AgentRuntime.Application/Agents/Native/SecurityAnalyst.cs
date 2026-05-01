using AgentRuntime.Core.Agents;

namespace AgentRuntime.Application.Agents.Native;

/// <summary>
/// A specialized native agent focused on cybersecurity operations.
/// This agent acts as a domain expert for investigating network-related security incidents,
/// identifying anomalies, and interpreting traffic patterns.
/// </summary>
public sealed class SecurityAnalyst : IAgent
{
    /// <inheritdoc />
    /// <value>"Security Analyst"</value>
    public string Name => "Security Analyst";

    /// <inheritdoc />
    /// <remarks>
    /// The persona is strictly scoped to network defense. In a typical workflow,
    /// this agent is often invoked by the <see cref="PlannerAgent"/> when the
    /// telemetry data indicates potential connection anomalies or lateral movement.
    /// </remarks>
    public string Instructions => "You are a security analyst specialized in network traffic analysis.";
}
