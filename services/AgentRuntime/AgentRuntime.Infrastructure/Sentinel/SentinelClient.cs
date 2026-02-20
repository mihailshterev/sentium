using System.Net.Http.Json;

namespace AgentRuntime.Infrastructure.Sentinel;

public sealed class SentinelClient
{
    private readonly HttpClient httpClient;

    public SentinelClient(HttpClient http)
    {
        httpClient = http;
    }

    public async Task<bool> IsAllowedAsync(
        string agentId,
        string action,
        IDictionary<string, string> data,
        CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync(
            "http://sentinel/evaluate",
            new
            {
                source = "Agent",
                category = "Agent",
                action,
                data
            },
            ct);

        response.EnsureSuccessStatusCode();

        var decision = await response.Content.ReadFromJsonAsync<Decision>(ct);
        return decision?.allowed ?? false;
    }

    private sealed record Decision(bool allowed, string reason);
}
