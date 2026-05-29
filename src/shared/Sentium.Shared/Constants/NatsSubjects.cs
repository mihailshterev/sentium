namespace Sentium.Shared.Constants;

public static class NatsSubjects
{
    /// <summary>
    /// Published by Registry when global app settings are persisted.
    /// All subscribing services evict their local L1 cache on receipt.
    /// </summary>
    public const string SettingsInvalidated = "registry.settings.invalidated";
}
