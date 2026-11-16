using Sentium.Core.Constants;
using Sentium.Registry.Contracts;
using Sentium.Registry.Contracts.Settings;

namespace Sentium.Praxis.Features.Registry;

/// <summary>
/// Reads the harness (per-user) and inference (global) settings from the Registry module in-process.
/// Registry caches every settings envelope itself (HybridCache) and evicts it on change,
/// so Praxis keeps no copy of its own.
/// </summary>
internal sealed class RegistrySettingsService(
    ISettingsService settingsService,
    ILogger<RegistrySettingsService> logger) : IRegistrySettingsService
{
    public async ValueTask<SettingsSnapshot> GetAsync(Guid? userId, CancellationToken ct = default)
    {
        HarnessSettingsSnapshot? harness;
        InferenceSettingsSnapshot? inference;

        try
        {
            var harnessTask = settingsService.GetValueAsync<HarnessSettings>(SettingsKeys.Harness, userId, ct);
            var inferenceTask = GetInferenceSettingsSafeAsync(ct);

            await Task.WhenAll(harnessTask, inferenceTask);

            harness = await harnessTask is { } h ? HarnessSettingsSnapshot.From(h) : null;
            inference = await inferenceTask;
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

        return new SettingsSnapshot(harness, inference);
    }

    private async Task<InferenceSettingsSnapshot?> GetInferenceSettingsSafeAsync(CancellationToken ct)
    {
        try
        {
            return await settingsService.GetValueAsync<InferenceSettings>(SettingsKeys.Inference, ct: ct) is { } settings
                ? InferenceSettingsSnapshot.From(settings)
                : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Inference settings not available from Registry; using defaults");
            return null;
        }
    }
}
