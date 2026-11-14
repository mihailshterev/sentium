namespace Sentium.Identity.Contracts;

/// <summary>
/// Read-only lookup of the household's users for modules that address people by role rather than by id. It needs no
/// HTTP context, so background workers can call it.
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// The ids of every user holding the Sovereign role.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetSovereignIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// The ids of every household member, Sovereigns included.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetHouseholdIdsAsync(CancellationToken ct = default);
}
