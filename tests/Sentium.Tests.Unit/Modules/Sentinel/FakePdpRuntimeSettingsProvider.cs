using Sentium.Sentinel.Features.Settings;

namespace Sentium.Tests.Unit.Modules.Sentinel;

internal sealed class FakePdpRuntimeSettingsProvider(PdpRuntimeSettings? settings = null) : IPdpRuntimeSettingsProvider
{
    public PdpRuntimeSettings Current { get; set; } = settings ?? new PdpRuntimeSettings();

    public ValueTask<PdpRuntimeSettings> GetAsync(CancellationToken ct = default) => ValueTask.FromResult(Current);
}
