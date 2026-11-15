namespace Sentium.Identity.Contracts;

/// <summary>
/// Implemented by modules that hold state or open connections for a Device. Identity calls every registered
/// observer after a Device is gone (removed by its owner or a Sovereign, unpaired by the app, or deleted with its
/// owner), when its token has already stopped working.
/// </summary>
public interface IDeviceRemovalObserver
{
    /// <summary>
    /// Must not throw and must not block: drop in-memory state and close connections only.
    /// </summary>
    void OnDeviceRemoved(Guid deviceId, Guid ownerUserId);
}
