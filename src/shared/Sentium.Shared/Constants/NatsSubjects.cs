namespace Sentium.Shared.Constants;

public static class NatsSubjects
{
    /// <summary>
    /// Published by Registry when global app settings are persisted.
    /// All subscribing services evict their local L1 cache on receipt.
    /// </summary>
    public const string SettingsInvalidated = "registry.settings.invalidated";

    /// <summary>
    /// Published by Watchdog on every monitored-target health state transition.
    /// Consumed by the Watchdog SSE stream to push live updates to the portal.
    /// </summary>
    public const string WatchdogStatusUpdates = "sentium.status.updates";

    /// <summary>
    /// Published by Watchdog when a new incident is opened for a target.
    /// </summary>
    public const string WatchdogIncidentOpened = "sentium.watchdog.incident.opened";

    /// <summary>
    /// Published by Watchdog when an open incident is resolved.
    /// </summary>
    public const string WatchdogIncidentResolved = "sentium.watchdog.incident.resolved";
}
