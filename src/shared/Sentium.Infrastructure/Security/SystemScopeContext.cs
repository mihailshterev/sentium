namespace Sentium.Infrastructure.Security;

/// <summary>
/// Scoped identity override for work outside an HTTP request. Background workers set it before resolving any EF or
/// service dependency, so the <see cref="CurrentUser"/> of that scope reports it without touching the singleton root
/// container: <see cref="Activate"/> grants unrestricted (system) data access, <see cref="ActAs"/> scopes the work to
/// one user exactly as that user's own request would be.
/// </summary>
public sealed class SystemScopeContext
{
    public bool IsActive { get; private set; }

    /// <summary>
    /// The user the scope's work runs for (set by <see cref="ActAs"/>).
    /// </summary>
    public Guid? ActingUserId { get; private set; }

    /// <summary>
    /// Marks this DI scope as a trusted system/background scope.
    /// </summary>
    public void Activate() => IsActive = true;

    /// <summary>
    /// Runs this DI scope's work as <paramref name="userId"/>: the per-user query filters apply, with no Sovereign or
    /// system bypass.
    /// </summary>
    public void ActAs(Guid userId) => ActingUserId = userId;
}
