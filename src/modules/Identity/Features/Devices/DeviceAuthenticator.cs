using Microsoft.EntityFrameworkCore;
using Sentium.Identity.Data;

namespace Sentium.Identity.Features.Devices;

internal sealed class DeviceAuthenticator(
    IdentityDbContext db,
    DeviceTokenCache tokenCache,
    TimeProvider timeProvider) : IDeviceAuthenticator
{
    public async Task<AuthenticatedDevice?> AuthenticateAsync(string token, string? appVersion, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var tokenHash = DeviceSecrets.Hash(token);

        var device = tokenCache.Get(tokenHash);
        if (device is null)
        {
            device = await LoadAsync(tokenHash, ct);
            if (device is null)
            {
                return null;
            }

            tokenCache.Set(tokenHash, device);
        }

        await TouchAsync(device, appVersion, ct);
        return device;
    }

    private async Task<AuthenticatedDevice?> LoadAsync(string tokenHash, CancellationToken ct)
    {
        var row = await db.Devices
            .AsNoTracking()
            .Where(d => d.TokenHash == tokenHash)
            .Select(d => new { d.Id, d.OwnerUserId, d.AppVersion, d.LastSeenAt })
            .SingleOrDefaultAsync(ct);

        return row is null ? null : new AuthenticatedDevice(row.Id, row.OwnerUserId, row.AppVersion, row.LastSeenAt);
    }

    private async Task TouchAsync(AuthenticatedDevice device, string? appVersion, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var version = appVersion ?? device.AppVersion;

        var isDue = device.LastSeenAt is not { } lastSeen || now - lastSeen >= DeviceLimits.LastSeenWriteInterval;
        if (!isDue && version == device.AppVersion)
        {
            return;
        }

        // Claim the write before awaiting it, so the Device's other in-flight requests do not repeat it.
        device.LastSeenAt = now;
        device.AppVersion = version;

        await db.Devices
            .Where(d => d.Id == device.DeviceId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSeenAt, now).SetProperty(d => d.AppVersion, version), ct);
    }
}
