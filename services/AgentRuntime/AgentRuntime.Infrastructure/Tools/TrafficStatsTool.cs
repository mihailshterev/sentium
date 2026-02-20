using AgentRuntime.Core.Tools;

namespace AgentRuntime.Infrastructure.Tools;

public sealed class TrafficStatsTool : IAgentTool
{
    public string Name => "traffic_stats";

    public string Description =>
        "Extract statistics such as IP counts, ports, and protocols from raw network traffic.";

    public Task<string> ExecuteAsync(
        string input,
        CancellationToken ct)
    {
        // Pretend parsing logic here
        var result =
            """
            {
              "uniqueIps": 14,
              "topPorts": [],
              "protocols": ["TCP", "UDP"]
            }
            """;

        return Task.FromResult(result);
    }
}
