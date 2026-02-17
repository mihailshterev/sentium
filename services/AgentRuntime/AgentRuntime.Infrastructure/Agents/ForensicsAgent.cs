using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;

namespace AgentRuntime.Infrastructure.Agents;

public sealed class ForensicsAgent : IAgent
{
    public string Name => "Forensics Investigator";
    public string Instructions => @"Analyze the 'activity' string for technical artifacts.
        Focus on:
        1. Base64 encoded PowerShell commands.
        2. Binary obfuscation (e.g., LOLBins like certutil or mshta).
        3. Specific registry or file system impact mentioned.
        Output your findings as a technical bulleted list.";
    public IEnumerable<IAgentTool> Tools => Array.Empty<IAgentTool>();
}
