namespace Sentium.Identity.Features.Users;

internal interface IUserService
{
    ValueTask<(IReadOnlyList<UserDto> Users, int TotalCount)> GetPagedUsersAsync(int page, int pageSize, CancellationToken ct);

    ValueTask<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken ct);

    Task<(bool Succeeded, string[] Errors)> UpdateProfileAsync(Guid userId, string firstName, string? lastName, string userName, CancellationToken ct);

    /// <summary>
    /// Fails when <paramref name="requesterId"/> is the target or the target is the last Sovereign.
    /// </summary>
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
