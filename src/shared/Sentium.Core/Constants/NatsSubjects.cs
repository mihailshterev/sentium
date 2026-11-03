namespace Sentium.Core.Constants;

public static class NatsSubjects
{
    /// <summary>
    /// Published by the Registry module when settings are persisted.
    /// Every Sentium.Web instance evicts its local L1 settings caches on receipt.
    /// </summary>
    public const string SettingsInvalidated = "registry.settings.invalidated";

    /// <summary>
    /// Published by Vigil on every monitored-target health state transition.
    /// Consumed by the Vigil SSE stream to push live updates to the portal.
    /// </summary>
    public const string WatchdogStatusUpdates = "sentium.status.updates";

    /// <summary>
    /// Published by Vigil when a new incident is opened for a target.
    /// </summary>
    public const string WatchdogIncidentOpened = "sentium.watchdog.incident.opened";

    /// <summary>
    /// Published by Vigil when an open incident is resolved.
    /// </summary>
    public const string WatchdogIncidentResolved = "sentium.watchdog.incident.resolved";
}
