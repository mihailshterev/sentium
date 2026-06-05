using Sentium.Sentinel.Core.Settings;

namespace Sentium.Tests.Unit.Sentinel;

/// <summary>Test double for <see cref="IPdpRuntimeSettingsProvider"/>.</summary>
internal sealed class FakePdpRuntimeSettingsProvider(PdpRuntimeSettings? settings = null) : IPdpRuntimeSettingsProvider
{
    public PdpRuntimeSettings Current { get; set; } = settings ?? new PdpRuntimeSettings();
    public PdpRuntimeSettings? LastUpdated { get; private set; }

    public ValueTask<PdpRuntimeSettings> GetAsync(CancellationToken ct = default) => ValueTask.FromResult(Current);

    public Task UpdateAsync(PdpRuntimeSettings settings, CancellationToken ct = default)
    {
        LastUpdated = settings;
        Current = settings;
        return Task.CompletedTask;
    }
}
