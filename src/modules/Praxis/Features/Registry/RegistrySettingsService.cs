using Sentium.Core.Constants;
using Sentium.Registry.Contracts;

namespace Sentium.Praxis.Features.Registry;

/// <summary>
/// Reads the harness (per-user) and Ollama (global) settings from the Registry module in-process.
/// Registry caches every settings envelope itself (HybridCache L1 + L2) and evicts it on change,
/// so Praxis keeps no copy of its own.
/// </summary>
public sealed class RegistrySettingsService(
    ISettingsService settingsService,
    ILogger<RegistrySettingsService> logger) : IRegistrySettingsService
{
    public async ValueTask<SettingsSnapshot> GetAsync(Guid? userId, CancellationToken ct = default)
    {
        HarnessSettingsSnapshot? harness;
        OllamaSettingsSnapshot? ollama;

        try
        {
            var harnessTask = settingsService.GetValueAsync<HarnessSettingsSnapshot>(SettingsKeys.Harness, userId, ct);
            var ollamaTask = GetOllamaSettingsSafeAsync(ct);

            await Task.WhenAll(harnessTask, ollamaTask);

            harness = await harnessTask;
            ollama = await ollamaTask;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to read settings from Registry for user {UserId}; using defaults", userId);
            return SettingsSnapshot.Default;
        }

        if (harness is null)
        {
            logger.LogWarning("Registry returned no harness settings for user {UserId}; using defaults", userId);
            return SettingsSnapshot.Default;
        }

        return new SettingsSnapshot(harness, ollama);
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
