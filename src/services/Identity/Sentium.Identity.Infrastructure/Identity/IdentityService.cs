using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Core.Entities;
using Sentium.Identity.Core.Security;
using Sentium.Infrastructure.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Logging;
using Sentium.Identity.Core.Constants;

namespace Sentium.Identity.Infrastructure.Identity;

public sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEventBus eventBus,
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

            await eventBus.PublishAsync(IdentityEvents.UserRegistered, new
            {
                UserId = user.Id,
                user.Email,
                CreatedAt = DateTime.UtcNow
            });

            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("User {UserId} registered and event published.", user.Id);
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
