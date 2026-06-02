namespace Sentium.Infrastructure.Security;

/// <summary>
/// Scoped flag that background workers set before resolving any EF/service dependencies so that
/// the <see cref="CurrentUser"/> in that scope reports <c>IsSystem = true</c>, granting unrestricted
/// data access to the worker without touching the singleton root container.
/// </summary>
public sealed class SystemScopeContext
{
    public bool IsActive { get; private set; }

    /// <summary>
    /// Marks this DI scope as a trusted system/background scope.
    /// </summary>
    public void Activate() => IsActive = true;
}
