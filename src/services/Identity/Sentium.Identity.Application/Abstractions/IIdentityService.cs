using Sentium.Identity.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;

namespace Sentium.Identity.Application.Abstractions;

/// <summary>
/// Orchestrates core identity flows including user registration and authentication.
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// Creates a new user account, assigns default roles, and initiates the first session.
    /// </summary>
    /// <param name="request">The registration details including email and password.</param>
    /// <remarks>
    /// Implementation note: Successful registration currently defaults the user to the <see cref="Roles.Sovereign"/> role.
    /// This method also publishes a 'user.registered' event to the system bus.
    /// </remarks>
    /// <returns>A tuple containing the identity result and the created user instance if successful.</returns>
    Task<(IdentityResult Result, ApplicationUser? User)> RegisterUserAsync(RegisterRequest request);

    /// <summary>
    /// Authenticates a user using email and password credentials.
    /// </summary>
    /// <param name="email">The user's registered email address.</param>
    /// <param name="password">The plain-text password to validate.</param>
    /// <returns>A <see cref="SignInResult"/> indicating success, lockout, or requirement for 2FA.</returns>
    Task<SignInResult> LoginAsync(string email, string password);
}
