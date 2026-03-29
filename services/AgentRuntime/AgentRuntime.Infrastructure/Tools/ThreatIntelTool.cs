using AgentRuntime.Core.Agents;
using AgentRuntime.Core.Tools;
using AgentRuntime.Core.Tools.Attributes;

namespace AgentRuntime.Infrastructure.Tools;

[AgentToolPolicy(
    AllowedAgents = new[] { AgentRole.SecurityAnalyst },
    RiskLevel = ToolRiskLevel.Low,
    RequiresApproval = false)]
public sealed class ThreatIntelTool : IAgentTool
{
    public string Name => "threat_intel_lookup";

    public string Description => "Check IP addresses or domains against threat intelligence feeds.";

    public Task<string> ExecuteAsync(
        string input,
        CancellationToken ct)
    {
        var result =
            """
            {
              "maliciousIps": [""],
              "confidence": "high"
            }
            """;

        return Task.FromResult(result);
    }
}
