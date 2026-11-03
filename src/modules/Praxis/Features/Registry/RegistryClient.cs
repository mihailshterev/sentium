using Sentium.Core.Constants;
using Sentium.Registry.Contracts;

namespace Sentium.Praxis.Features.Registry;

/// <summary>
/// Reads the agent settings from the Registry module in-process (<see cref="ISettingsService"/>).
/// </summary>
public sealed class RegistryClient(ISettingsService settingsService, ILogger<RegistryClient> logger) : IRegistryClient
{
    public async Task<SettingsSnapshot?> GetSettingsAsync(Guid? userId, CancellationToken ct = default)
    {
        try
        {
            var harnessTask = settingsService.GetValueAsync<HarnessSettingsSnapshot>(SettingsKeys.Harness, userId, ct);
            var ollamaTask = GetOllamaSettingsSafeAsync(ct);

            await Task.WhenAll(harnessTask, ollamaTask);

            var harness = await harnessTask;
            if (harness is null)
            {
                return null;
            }

            return new SettingsSnapshot(harness, await ollamaTask);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to read settings from Registry for user {UserId}; using defaults", userId);
            return null;
        }
    }

    private async Task<OllamaSettingsSnapshot?> GetOllamaSettingsSafeAsync(CancellationToken ct)
    {
        try
        {
            return await settingsService.GetValueAsync<OllamaSettingsSnapshot>(SettingsKeys.Ollama, ct: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Ollama settings not available from Registry; using defaults");
            return null;
        }
    }
}
