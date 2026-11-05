namespace Sentium.Vigil.Features.Settings;

public sealed record VigilRuntimeSettings
{
    public int PollIntervalSeconds { get; init; } = 15;
    public int ProbeTimeoutSeconds { get; init; } = 5;
    public int DegradedLatencyMs { get; init; } = 1_000;
    public int ConsecutiveFailuresToOpenIncident { get; init; } = 3;
    public int SampleHistorySize { get; init; } = 60;
}

/// <summary>
/// Supplies the current vigil runtime settings (poll interval, thresholds, history size).
/// </summary>
public interface IVigilSettingsProvider
{
    ValueTask<VigilRuntimeSettings> GetAsync(CancellationToken ct = default);
}
