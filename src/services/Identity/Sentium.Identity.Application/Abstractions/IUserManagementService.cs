using Sentium.Identity.Core.Entities;

namespace Sentium.Identity.Application.Abstractions;

/// <summary>
/// Provides administrative and self-service user management operations.
/// </summary>
public interface IUserManagementService
{
    /// <summary>
    /// Retrieves a complete list of all users in the system.
    /// </summary>
    Task<IReadOnlyList<ApplicationUser>> GetAllUsersAsync(CancellationToken ct);

    /// <summary>
    /// Finds a specific user by their unique identifier.
    /// </summary>
    Task<ApplicationUser?> GetUserByIdAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Updates the core profile information for a user.
    /// </summary>
    /// <remarks>
    /// If the email is changed, the system automatically synchronizes the UserName
    /// to match the new email address.
    /// </remarks>
    /// <returns>A result indicating success or a list of validation/identity errors.</returns>
    Task<(bool Succeeded, string[] Errors)> UpdateProfileAsync(Guid userId, string firstName, string? lastName, string email, CancellationToken ct);

    /// <summary>
    /// Permanently deletes a user from the system.
    /// </summary>
    /// <param name="requesterId">The ID of the user performing the deletion (used for self-deletion checks).</param>
    /// <param name="targetUserId">The ID of the user to be removed.</param>
    /// <remarks>
    /// This method enforces critical safety rules:
    /// 1. A user cannot delete themselves.
    /// 2. The system will prevent the deletion of the final user with the 'Sovereign' role.
    /// </remarks>
    Task<(bool Succeeded, string[] Errors)> DeleteUserAsync(Guid requesterId, Guid targetUserId, CancellationToken ct);
}
