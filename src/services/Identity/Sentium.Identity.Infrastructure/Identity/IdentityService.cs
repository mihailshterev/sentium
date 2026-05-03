using Sentium.Identity.Application.Abstractions;
using Sentium.Identity.Core.Entities;
using Sentium.Infrastructure.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Logging;

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
            FirstName = request.Email,
            LastName = request.Email
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (result.Succeeded)
        {
            await signInManager.SignInAsync(user, isPersistent: false);

            await eventBus.PublishAsync("identity.user.registered", new
            {
                UserId = user.Id,
                Email = user.Email,
                CreatedAt = DateTime.UtcNow
            });

            logger.LogInformation("User {UserId} registered and event published.", user.Id);
        }

        return (result, user);
    }

    public async Task<SignInResult> LoginAsync(string email, string password)
    {
        return await signInManager.PasswordSignInAsync(email, password, false, lockoutOnFailure: true);
    }
}
