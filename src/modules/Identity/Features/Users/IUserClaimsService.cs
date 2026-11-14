using System.Security.Claims;

namespace Sentium.Identity.Features.Users;

/// <summary>
/// Provides identity and security claims for users.
/// Centralizes the mapping between domain entities and <see cref="Claim"/> objects.
/// </summary>
internal interface IUserClaimsService
{
    /// <summary>
    /// Retrieves claims for a specific user based on requested scopes.
    /// </summary>
    Task<IReadOnlyCollection<Claim>> GetClaimsAsync(Guid userId, IEnumerable<string> scopes, CancellationToken ct);

    /// <summary>
    /// Retrieves a mapping of claims for multiple users simultaneously.
    /// Optimized for listing views to prevent N+1 query patterns.
    /// </summary>
    /// <param name="scopes">The identity/API scopes to include (e.g., <see cref="Security.Scopes.Roles"/>).</param>
    Task<IDictionary<Guid, IReadOnlyCollection<Claim>>> GetBatchClaimsAsync(IEnumerable<Guid> userIds, IEnumerable<string> scopes, CancellationToken ct);
}
