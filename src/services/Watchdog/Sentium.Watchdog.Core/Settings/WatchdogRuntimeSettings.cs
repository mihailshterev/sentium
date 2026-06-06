namespace Sentium.Watchdog.Core.Settings;

public sealed record WatchdogRuntimeSettings
{
    public int PollIntervalSeconds { get; init; } = 15;
    public int ProbeTimeoutSeconds { get; init; } = 5;
    public int DegradedLatencyMs { get; init; } = 1_000;
    public int ConsecutiveFailuresToOpenIncident { get; init; } = 3;
    public int SampleHistorySize { get; init; } = 60;
}

public interface IWatchdogSettingsProvider
{
    ValueTask<WatchdogRuntimeSettings> GetAsync(CancellationToken ct = default);
}
