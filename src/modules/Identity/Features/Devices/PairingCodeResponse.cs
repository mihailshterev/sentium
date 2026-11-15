namespace Sentium.Identity.Features.Devices;

internal sealed record PairingCodeResponse(string Code, DateTimeOffset ExpiresAt);
