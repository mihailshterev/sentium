namespace Sentium.Registry.Contracts;

/// <summary>
/// Lets a module react when settings are saved, for work a cache eviction cannot do (for example recomputing stored
/// times after a time zone change). Handlers run after the change is committed, one after another; a failing
/// handler is logged and never fails the update.
/// </summary>
public interface ISettingsChangedHandler
{
    /// <param name="userId">The user whose per-user settings changed; <see langword="null"/> for a global key.</param>
    Task OnSettingsChangedAsync(string key, Guid? userId, CancellationToken ct = default);
}
