namespace Sentium.Identity.Application.Abstractions;

/// <summary>
/// Manages user roles and enforces hierarchical security constraints.
/// </summary>
public interface IRoleService
{
    /// <summary>
    /// Assigns a role to a user, strictly enforcing hierarchy permissions.
    /// </summary>
    /// <param name="requesterId">The ID of the user attempting the assignment.</param>
    /// <param name="targetUserId">The ID of the user receiving the role.</param>
    /// <param name="roleName">The name of the role to assign (must exist in <see cref="Roles"/>).</param>
    /// <remarks>
    /// Security Policy:
    /// 1. Only users with the <see cref="Roles.Sovereign"/> role can assign roles.
    /// 2. Only a Sovereign can promote another user to Sovereign.
    /// </remarks>
    Task<(bool Succeeded, string? Error)> AssignRoleAsync(Guid requesterId, Guid targetUserId, string roleName, CancellationToken ct);

    /// <summary>
    /// Removes a role from a user.
    /// </summary>
    /// <remarks>
    /// Safety Logic:
    /// Prevents the removal of the final <see cref="Roles.Sovereign"/> to ensure the system remains administrable.
    /// </remarks>
    Task<(bool Succeeded, string? Error)> RemoveRoleAsync(Guid requesterId, Guid targetUserId, string roleName, CancellationToken ct);

    /// <summary>
    /// Retrieves all roles currently assigned to a user.
    /// </summary>
    ValueTask<IList<string>> GetRolesAsync(Guid userId, CancellationToken ct);
}
