using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Sentium.AgentRuntime.Core.Registry;
using Sentium.Shared.Constants;

namespace Sentium.AgentRuntime.Infrastructure.Registry;

public sealed class RegistryClient(HttpClient httpClient, ILogger<RegistryClient> logger) : IRegistryClient
{
    public async Task<SettingsSnapshot?> GetSettingsAsync(Guid? userId, CancellationToken ct = default)
    {
        try
        {
            var harnesspath = userId.HasValue ? $"settings/{SettingsKeys.Harness}?userId={userId.Value}" : $"settings/{SettingsKeys.Harness}";

            var harnessTask = httpClient.GetFromJsonAsync<SettingsEnvelope<HarnessSettingsSnapshot>>(harnesspath, ct);
            var ollamaTask = FetchOllamaSettingsSafeAsync(ct);

            await Task.WhenAll(harnessTask, ollamaTask);

            var harness = (await harnessTask)?.Value;
            if (harness is null)
            {
                return null;
            }

            return new SettingsSnapshot(harness, await ollamaTask);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch settings from Registry for user {UserId}; using defaults", userId);
            return null;
        }
    }

    private async Task<OllamaSettingsSnapshot?> FetchOllamaSettingsSafeAsync(CancellationToken ct)
    {
        try
        {
            var envelope = await httpClient.GetFromJsonAsync<SettingsEnvelope<OllamaSettingsSnapshot>>($"settings/{SettingsKeys.Ollama}", ct);
            return envelope?.Value;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Ollama settings not available from Registry; using defaults");
            return null;
        }
    }
}
