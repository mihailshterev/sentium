namespace Sentium.Identity.Features.Devices;

/// <summary>
/// What is needed to forget a Device after its row is gone: the cache key of its token and whom to tell.
/// </summary>
internal sealed record RemovedDevice(Guid DeviceId, Guid OwnerUserId, string TokenHash);
