using AgentRuntime.Core.Agents;

namespace AgentRuntime.Application.Agents.Native;

/// <summary>
/// A specialized native agent dedicated to deep technical artifact analysis.
/// This agent acts as a digital forensics expert, capable of deconstructing
/// obfuscated commands and identifying suspicious system-level interactions.
/// </summary>
public sealed class ForensicsAgent : IAgent
{
    /// <inheritdoc />
    /// <value>"Forensics Investigator"</value>
    public string Name => "Forensics Investigator";

    /// <inheritdoc />
    /// <remarks>
    /// This agent is optimized for technical pattern matching. It specifically targets
    /// Living-off-the-Land Binaries (LOLBins) and common malware persistence techniques.
    /// It is often invoked when raw log data contains encoded strings or suspicious
    /// process execution arguments.
    /// </remarks>
    public string Instructions => @"Analyze the 'activity' string for technical artifacts.
        Focus on:
        1. Base64 encoded PowerShell commands.
        2. Binary obfuscation (e.g., LOLBins like certutil or mshta).
        3. Specific registry or file system impact mentioned.
        Output your findings as a technical bulleted list.";
}
