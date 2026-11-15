using Microsoft.EntityFrameworkCore;
using Sentium.Core.Results;
using Sentium.Identity.Contracts;
using Sentium.Identity.Data;
using Sentium.Identity.Entities;

namespace Sentium.Identity.Features.Devices;

internal sealed class DeviceService(
    IdentityDbContext db,
    DeviceTokenCache tokenCache,
    IEnumerable<IDeviceRemovalObserver> removalObservers,
    TimeProvider timeProvider,
    ILogger<DeviceService> logger) : IDeviceService
{
    private const string InvalidCodeError = "The pairing code is invalid or has expired.";

    private static readonly string DeviceLimitError =
        $"You already have {DeviceLimits.MaxDevicesPerUser} devices. Remove one before pairing another.";

    public async Task<Result<PairingCodeResponse>> CreatePairingCodeAsync(Guid userId, CancellationToken ct)
    {
        if (await HasReachedDeviceLimitAsync(userId, ct))
        {
            return Result<PairingCodeResponse>.Conflict(DeviceLimitError);
        }

        var now = timeProvider.GetUtcNow();

        // One live code per user; expired codes of anyone are swept here rather than by a worker.
        await db.DevicePairingCodes
            .Where(c => c.UserId == userId || c.ExpiresAt <= now)
            .ExecuteDeleteAsync(ct);

        var code = DeviceSecrets.NewPairingCode();
        var pairingCode = new DevicePairingCode
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            CodeHash = DeviceSecrets.Hash(code),
            CreatedAt = now,
            ExpiresAt = now + DeviceLimits.PairingCodeLifetime
        };

        db.DevicePairingCodes.Add(pairingCode);
        await db.SaveChangesAsync(ct);

        return Result<PairingCodeResponse>.Success(new PairingCodeResponse(code, pairingCode.ExpiresAt));
    }

    public async Task<Result<PairDeviceResponse>> PairAsync(string code, string name, string? appVersion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var now = timeProvider.GetUtcNow();
        var codeHash = DeviceSecrets.Hash(DeviceSecrets.NormalizePairingCode(code));

        var pairingCode = await db.DevicePairingCodes
            .AsNoTracking()
            .Where(c => c.CodeHash == codeHash)
            .Select(c => new { c.Id, c.UserId, c.ExpiresAt })
            .SingleOrDefaultAsync(ct);

        if (pairingCode is null || pairingCode.ExpiresAt <= now)
        {
            return Result<PairDeviceResponse>.Invalid(InvalidCodeError);
        }

        // The delete is the single-use gate: of two requests racing with one code, only one deletes the row.
        var spent = await db.DevicePairingCodes.Where(c => c.Id == pairingCode.Id).ExecuteDeleteAsync(ct);
        if (spent == 0)
        {
            return Result<PairDeviceResponse>.Invalid(InvalidCodeError);
        }

        if (await HasReachedDeviceLimitAsync(pairingCode.UserId, ct))
        {
            return Result<PairDeviceResponse>.Conflict(DeviceLimitError);
        }

        var token = DeviceSecrets.NewToken();
        var device = new Device
        {
            Id = Guid.CreateVersion7(),
            OwnerUserId = pairingCode.UserId,
            Name = name.Trim(),
            TokenHash = DeviceSecrets.Hash(token),
            AppVersion = appVersion,
            CreatedAt = now
        };

        db.Devices.Add(device);
        await db.SaveChangesAsync(ct);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Device {DeviceId} paired for user {UserId}.", device.Id, device.OwnerUserId);
        }

        return Result<PairDeviceResponse>.Success(new PairDeviceResponse(device.Id, token));
    }

    public async Task<IReadOnlyList<DeviceInfo>> ListAsync(Guid? ownerUserId, CancellationToken ct)
    {
        return await db.Devices
            .AsNoTracking()
            .Where(d => ownerUserId == null || d.OwnerUserId == ownerUserId)
            .OrderBy(d => d.Name)
            .ThenBy(d => d.CreatedAt)
            .Select(d => new DeviceInfo(d.Id, d.Name, d.OwnerUserId, d.Owner.UserName, d.AppVersion, d.CreatedAt, d.LastSeenAt))
            .ToListAsync(ct);
    }

    public async Task<bool> RemoveAsync(Guid deviceId, Guid? ownerUserId, CancellationToken ct)
    {
        var device = await db.Devices
            .AsNoTracking()
            .Where(d => d.Id == deviceId && (ownerUserId == null || d.OwnerUserId == ownerUserId))
            .Select(d => new RemovedDevice(d.Id, d.OwnerUserId, d.TokenHash))
            .SingleOrDefaultAsync(ct);

        if (device is null)
        {
            return false;
        }

        await db.Devices.Where(d => d.Id == deviceId).ExecuteDeleteAsync(ct);
        Forget([device]);

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Device {DeviceId} of user {UserId} removed.", device.DeviceId, device.OwnerUserId);
        }

        return true;
    }

    public async Task<IReadOnlyList<RemovedDevice>> GetOwnedAsync(Guid ownerUserId, CancellationToken ct)
    {
        return await db.Devices
            .AsNoTracking()
            .Where(d => d.OwnerUserId == ownerUserId)
            .Select(d => new RemovedDevice(d.Id, d.OwnerUserId, d.TokenHash))
            .ToListAsync(ct);
    }

    public void Forget(IReadOnlyList<RemovedDevice> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);

        foreach (var device in devices)
        {
            tokenCache.Evict(device.TokenHash);

            foreach (var observer in removalObservers)
            {
                observer.OnDeviceRemoved(device.DeviceId, device.OwnerUserId);
            }
        }
    }

    private async Task<bool> HasReachedDeviceLimitAsync(Guid userId, CancellationToken ct)
        => await db.Devices.CountAsync(d => d.OwnerUserId == userId, ct) >= DeviceLimits.MaxDevicesPerUser;
}
