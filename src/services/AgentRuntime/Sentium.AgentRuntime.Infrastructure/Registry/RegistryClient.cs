using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Sentium.AgentRuntime.Core.Registry;

namespace Sentium.AgentRuntime.Infrastructure.Registry;

public sealed class RegistryClient(HttpClient httpClient, ILogger<RegistryClient> logger) : IRegistryClient
{
    public async Task<SettingsSnapshot?> GetSettingsAsync(CancellationToken ct = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<SettingsSnapshot>("settings", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch settings from Registry; using defaults");
            return null;
        }
    }
}
