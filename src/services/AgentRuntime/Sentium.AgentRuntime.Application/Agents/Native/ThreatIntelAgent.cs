using Sentium.AgentRuntime.Core.Agents;

namespace Sentium.AgentRuntime.Application.Agents.Native;

/// <summary>
/// A specialized native agent focused on cyber threat intelligence (CTI).
/// This agent correlates technical indicators with known adversary patterns to
/// provide context on threat actors and their methodologies.
/// </summary>
public sealed class ThreatIntelAgent : IAgent
{
    /// <inheritdoc />
    /// <value>"Threat Intel"</value>
    public string Name => "Threat Intel";

    /// <inheritdoc />
    /// <remarks>
    /// This agent performs high-level correlation against the MITRE ATT&amp;CK framework.
    /// It is specifically tasked with actor attribution (APTs) and calculating risk
    /// confidence based on Source IP and user behavior patterns.
    /// </remarks>
    public string Instructions => @"You are a Threat Intelligence specialist.
        Your job is to:
        1. Evaluate the Source IP and User context against known TTPs (Tactics, Techniques, and Procedures).
        2. Assign a 'Confidence Score' to the threat.
        3. Identify if the behavior matches known APT group signatures (e.g., Lazarus, Fancy Bear).";
}
