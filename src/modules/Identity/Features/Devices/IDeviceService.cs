using Sentium.Core.Results;
using Sentium.Identity.Contracts;

namespace Sentium.Identity.Features.Devices;

internal interface IDeviceService
{
    /// <summary>
    /// Creates the user's pairing code, replacing any earlier one. A conflict means the user already has the maximum
    /// number of Devices.
    /// </summary>
    Task<Result<PairingCodeResponse>> CreatePairingCodeAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Exchanges a pairing code for a new Device and its token. The code is spent even when pairing then fails.
    /// </summary>
    Task<Result<PairDeviceResponse>> PairAsync(string code, string name, string? appVersion, CancellationToken ct);

    /// <summary>
    /// The Devices owned by <paramref name="ownerUserId"/>, or every Device when it is <see langword="null"/>.
    /// </summary>
    Task<IReadOnlyList<DeviceInfo>> ListAsync(Guid? ownerUserId, CancellationToken ct);

    /// <summary>
    /// Removes a Device and kills its token. When <paramref name="ownerUserId"/> is given, the Device must belong to
    /// that user; returns <see langword="false"/> when no such Device exists.
    /// </summary>
    Task<bool> RemoveAsync(Guid deviceId, Guid? ownerUserId, CancellationToken ct);

    /// <summary>
    /// The Devices that will disappear together with the account of <paramref name="ownerUserId"/>; take it before
    /// deleting the user and pass it to <see cref="Forget"/> afterwards.
    /// </summary>
    Task<IReadOnlyList<RemovedDevice>> GetOwnedAsync(Guid ownerUserId, CancellationToken ct);

    /// <summary>
    /// Evicts the cached tokens of Devices whose rows are already gone and tells the other modules.
    /// </summary>
    void Forget(IReadOnlyList<RemovedDevice> devices);
}
