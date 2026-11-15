namespace Sentium.Identity.Features.Devices;

/// <param name="Token">The Device credential. This response is the only time it leaves the server.</param>
internal sealed record PairDeviceResponse(Guid DeviceId, string Token);
