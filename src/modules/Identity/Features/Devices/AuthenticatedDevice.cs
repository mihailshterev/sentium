namespace Sentium.Identity.Features.Devices;

/// <summary>
/// A cached token lookup. <see cref="LastSeenAt"/> and <see cref="AppVersion"/> track what was last persisted, so the
/// authenticator can tell when the row is due another write.
/// </summary>
internal sealed class AuthenticatedDevice(Guid deviceId, Guid ownerUserId, string? appVersion, DateTimeOffset? lastSeenAt)
{
    public Guid DeviceId { get; } = deviceId;

    public Guid OwnerUserId { get; } = ownerUserId;

    public string? AppVersion { get; set; } = appVersion;

    public DateTimeOffset? LastSeenAt { get; set; } = lastSeenAt;
}
