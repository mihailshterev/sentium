namespace Sentium.Identity.Contracts;

/// <summary>
/// Implemented by modules that keep data a user owns. Identity calls every registered observer after an account is
/// deleted, so no module keeps (or keeps acting on) data of a user who no longer exists.
/// </summary>
public interface IUserRemovalObserver
{
    /// <summary>
    /// Deletes everything the module holds for <paramref name="userId"/>. Runs after the account is gone, in no
    /// particular order with the other observers; a failure is logged and does not stop them.
    /// </summary>
    Task OnUserRemovedAsync(Guid userId, CancellationToken ct);
}
