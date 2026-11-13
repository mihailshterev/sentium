using Microsoft.AspNetCore.Identity;
using Sentium.Identity.Entities;

namespace Sentium.Identity.Features.Account;

/// <summary>
/// Orchestrates core identity flows including user registration and authentication.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Creates a new user account, assigns default roles, and initiates the first session.
    /// </summary>
    /// <param name="request">The registration details including username and password.</param>
    /// <remarks>
    /// Implementation note: Successful registration currently defaults the user to the <see cref="Roles.Sovereign"/> role.
    /// This method also publishes a 'user.registered' event to the system bus.
    /// </remarks>
    /// <returns>A tuple containing the identity result and the created user instance if successful.</returns>
    Task<(IdentityResult Result, ApplicationUser? User)> RegisterUserAsync(RegisterRequest request);

    /// <summary>
    /// Authenticates a user using username and password credentials.
    /// </summary>
    /// <param name="userName">The user's username.</param>
    /// <param name="password">The plain-text password to validate.</param>
    /// <returns>A <see cref="SignInResult"/> indicating success, lockout, or requirement for 2FA.</returns>
    Task<SignInResult> LoginAsync(string userName, string password);

    /// <summary>
    /// Ends the current session by clearing the Identity auth cookie.
    /// </summary>
    Task LogoutAsync();
}
