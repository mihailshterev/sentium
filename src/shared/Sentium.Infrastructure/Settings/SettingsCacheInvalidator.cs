using Microsoft.Extensions.Logging;

namespace Sentium.Infrastructure.Settings;

/// <summary>
/// Fans a settings change out to every module's <see cref="ISettingsCacheInvalidationHandler"/> so each
/// evicts its own cache entries for that settings identity.
/// </summary>
public interface ISettingsCacheInvalidator
{
    Task InvalidateAsync(string settingsKey, Guid? userId, CancellationToken ct = default);
}

/// <summary>
/// Calls every registered handler in-process. A failing handler is logged and never prevents the others
/// from evicting (the change is already persisted; a stale entry still expires with its cache TTL).
/// </summary>
public sealed class SettingsCacheInvalidator(
    IEnumerable<ISettingsCacheInvalidationHandler> handlers,
    ILogger<SettingsCacheInvalidator> logger) : ISettingsCacheInvalidator
{
    private readonly ISettingsCacheInvalidationHandler[] _handlers = [.. handlers];

    public async Task InvalidateAsync(string settingsKey, Guid? userId, CancellationToken ct = default)
    {
        foreach (var handler in _handlers)
        {
            try
            {
                await handler.TryInvalidateAsync(settingsKey, userId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Handler {Handler} failed to evict settings '{Key}' (scope {Scope})",
                    handler.GetType().Name, settingsKey, userId?.ToString() ?? "global");
            }
        }
    }
}
