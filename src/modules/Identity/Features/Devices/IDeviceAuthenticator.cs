namespace Sentium.Identity.Features.Devices;

internal interface IDeviceAuthenticator
{
    /// <summary>
    /// Resolves a Device token, or returns <see langword="null"/> when no Device holds it. A successful call also
    /// records that the Device was seen (and its app version, when given), throttled to one write every few minutes.
    /// </summary>
    Task<AuthenticatedDevice?> AuthenticateAsync(string token, string? appVersion, CancellationToken ct);
}
