using Microsoft.AspNetCore.Identity;
using Sentium.Identity.Entities;
using Sentium.Identity.Features.Security;

namespace Sentium.Identity.Features.Account;

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
            UserName = request.UserName.Trim(),
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

    public async Task<SignInResult> LoginAsync(string userName, string password)
    {
        var result = await signInManager.PasswordSignInAsync(userName.Trim(), password, false, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            logger.LogWarning("Login attempt for {UserName} rejected: account locked.", userName);
        }
        else if (!result.Succeeded)
        {
            logger.LogInformation("Failed login attempt for {UserName}.", userName);
        }

        return result;
    }

    public Task LogoutAsync() => signInManager.SignOutAsync();
}
