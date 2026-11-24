using Microsoft.AspNetCore.Identity;

namespace Sentium.Identity.Features.Account;

/// <summary>
/// Orchestrates core identity flows including user registration and authentication.
/// </summary>
internal interface IIdentityService
{
    /// <summary>
    /// Whether an account can register now: always the first account of an installation, otherwise only while a
    /// Sovereign keeps registration open (the Registry <c>access</c> setting).
    /// </summary>
    Task<RegistrationStatusResponse> GetRegistrationStatusAsync(CancellationToken ct);

    /// <summary>
    /// Creates an account and signs it in. The first account of an installation becomes the Sovereign; later ones are
    /// Members. Reports <see cref="RegistrationOutcome.Closed"/> instead when registration is closed.
    /// </summary>
    Task<RegistrationOutcome> RegisterUserAsync(RegisterRequest request, CancellationToken ct);

    /// <returns>A <see cref="SignInResult"/> indicating success, lockout, or requirement for 2FA.</returns>
    Task<SignInResult> LoginAsync(string userName, string password);

    /// <summary>
    /// Ends the current session by clearing the Identity auth cookie.
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// Changes the user's password and re-issues this session's cookie. The account's other sessions end within the
    /// security-stamp validation interval; paired Devices keep their own tokens.
    /// </summary>
    Task<IdentityResult> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);

    /// <summary>
    /// Ends every other session of the account (within the security-stamp validation interval) and keeps this one.
    /// </summary>
    /// <returns><see langword="false"/> when the user does not exist.</returns>
    Task<bool> SignOutOtherSessionsAsync(Guid userId);
}
