using Sentium.Identity.Features.Abstractions;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Logging;

namespace Sentium.Identity.Features.Services;

public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<IdentityService> logger) : IIdentityService
{
    public async Task<(IdentityResult Result, ApplicationUser? User)> RegisterUserAsync(RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = string.Empty,
            LastName = null
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (result.Succeeded)
        {
            var roleResult = await userManager.AddToRoleAsync(user, Roles.Member);
            if (!roleResult.Succeeded)
            {
                logger.LogError("Failed to assign role {Role} to user {UserId}", Roles.Member, user.Id);
                return (roleResult, user);
            }

            await signInManager.SignInAsync(user, isPersistent: false);

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("User {UserId} registered.", user.Id);
            }
        }

        return (result, user);
    }

    public async Task<SignInResult> LoginAsync(string email, string password)
    {
        var result = await signInManager.PasswordSignInAsync(email, password, false, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            logger.LogWarning("Login attempt for {Email} rejected: account locked.", email);
        }
        else if (!result.Succeeded)
        {
            logger.LogInformation("Failed login attempt for {Email}.", email);
        }

        return result;
    }
}
