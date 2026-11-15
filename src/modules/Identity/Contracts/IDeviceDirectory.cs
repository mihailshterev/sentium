namespace Sentium.Identity.Contracts;

/// <summary>
/// Read-only lookup of paired Devices for modules that show or track them. It needs no HTTP context and applies no
/// visibility rule of its own: the caller decides whose Devices to ask for.
/// </summary>
public interface IDeviceDirectory
{
    /// <summary>
    /// The Devices owned by <paramref name="ownerUserId"/>, or every Device when it is <see langword="null"/>.
    /// </summary>
    Task<IReadOnlyList<DeviceInfo>> ListAsync(Guid? ownerUserId, CancellationToken ct = default);
}
