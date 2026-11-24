namespace Sentium.Identity.Features.Users;

/// <summary>
/// Provides administrative and self-service user management operations.
/// </summary>
internal interface IUserService
{
    /// <summary>
    /// Retrieves a paginated list of users in the system.
    /// </summary>
    ValueTask<(IReadOnlyList<UserDto> Users, int TotalCount)> GetPagedUsersAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>
    /// Finds a specific user by their unique identifier.
    /// </summary>
    ValueTask<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct);

    /// <summary>
    /// Updates the core profile information for a user.
    /// </summary>
    Task<(bool Succeeded, string[] Errors)> UpdateProfileAsync(Guid userId, string firstName, string? lastName, string userName, CancellationToken ct);

    /// <summary>
    /// Permanently deletes a user from the system.
    /// </summary>
    /// <param name="requesterId">The ID of the user performing the deletion (used for self-deletion checks).</param>
    /// <remarks>
    /// This method enforces critical safety rules:
    /// 1. A user cannot delete themselves.
    /// 2. The system will prevent the deletion of the final user with the 'Sovereign' role.
    /// </remarks>
    Task<(bool Succeeded, string[] Errors)> DeleteUserAsync(Guid requesterId, Guid targetUserId, CancellationToken ct);

    /// <summary>
    /// Sets a new password without the current one, clears any lockout and ends the account's browser sessions.
    /// </summary>
    Task<(bool Succeeded, string[] Errors)> ResetPasswordAsync(Guid targetUserId, string newPassword, CancellationToken ct);

    /// <summary>
    /// Clears an account's lockout and its failed sign-in count.
    /// </summary>
    /// <returns><see langword="false"/> when the user does not exist.</returns>
    Task<bool> UnlockAsync(Guid targetUserId, CancellationToken ct);
}
