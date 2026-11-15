using Sentium.Identity.Contracts;

namespace Sentium.Identity.Features.Devices;

/// <summary>
/// Singleton facade for other modules; each call opens its own DI scope for the Identity DbContext.
/// </summary>
internal sealed class ScopedDeviceDirectory(IServiceScopeFactory scopeFactory) : IDeviceDirectory
{
    public async Task<IReadOnlyList<DeviceInfo>> ListAsync(Guid? ownerUserId, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        return await scope.ServiceProvider.GetRequiredService<IDeviceService>().ListAsync(ownerUserId, ct);
    }
}
