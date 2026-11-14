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
    /// A newly registered user gets the <see cref="Security.Roles.Member"/> role.
    /// </remarks>
    Task<(IdentityResult Result, ApplicationUser? User)> RegisterUserAsync(RegisterRequest request);

    /// <summary>
    /// Authenticates a user using username and password credentials.
    /// </summary>
    /// <param name="password">The plain-text password to validate.</param>
    /// <returns>A <see cref="SignInResult"/> indicating success, lockout, or requirement for 2FA.</returns>
    Task<SignInResult> LoginAsync(string userName, string password);

    /// <summary>
    /// Ends the current session by clearing the Identity auth cookie.
    /// </summary>
    Task LogoutAsync();
}
