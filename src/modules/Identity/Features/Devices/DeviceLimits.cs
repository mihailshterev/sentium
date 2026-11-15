namespace Sentium.Identity.Features.Devices;

internal static class DeviceLimits
{
    public const int MaxDevicesPerUser = 10;
    public const int NameMax = 64;
    public const int AppVersionMax = 32;
    public const int PairingCodeLength = 8;

    /// <summary>
    /// Room for a code typed with separators or spaces.
    /// </summary>
    public const int PairingCodeInputMax = 32;

    public static readonly TimeSpan PairingCodeLifetime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// A Device calls every few seconds; its last-seen time is persisted no more often than this.
    /// </summary>
    public static readonly TimeSpan LastSeenWriteInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long a token lookup is trusted without the database. Removal evicts the entry, so this only bounds the
    /// window for changes made behind the service's back.
    /// </summary>
    public static readonly TimeSpan TokenCacheLifetime = TimeSpan.FromMinutes(1);
}
